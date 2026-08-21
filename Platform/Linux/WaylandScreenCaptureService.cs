using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;

namespace CrosshairOverlay.Platform.Linux;

/// <summary>
/// Owns the screen capture session used by motion detection on Linux.
///
/// Wayland compositors never hand out screen content without consent, so a session is only opened
/// while motion detection is enabled, and enabling it again always asks for permission again — no
/// restore token is stored. The session survives changes to the capture region: only the GStreamer
/// pipeline that crops it is restarted.
/// </summary>
public sealed class WaylandScreenCaptureService : IScreenCaptureAccess, IDisposable
{
    private readonly object _gate = new();
    private readonly Func<IReadOnlyList<LinuxMonitor>> _monitorProvider;

    // One reader per PipeWire node: overlays on different monitors pull different regions, and a
    // single shared reader would be torn down and restarted on every alternating call.
    private readonly Dictionary<uint, ActiveReader> _readers = [];

    // Nodes whose pipeline died. Without this a dead node would be respawned on every capture tick.
    private readonly HashSet<uint> _brokenNodes = [];
    private readonly HashSet<string> _warnings = [];

    private ScreenCastSession? _session;
    private CancellationTokenSource? _requestCancellation;
    private int _targetFramesPerSecond = 30;
    private int _disposed;

    public WaylandScreenCaptureService(Func<IReadOnlyList<LinuxMonitor>> monitorProvider)
    {
        _monitorProvider = monitorProvider;
    }

    public bool RequiresPermission => true;

    public ScreenCaptureAccessState State { get; private set; } = ScreenCaptureAccessState.Inactive;

    public string? StateDetail { get; private set; }

    public event EventHandler? StateChanged;

    /// <summary>Rate the capture pipeline is asked to deliver; changing it restarts the pipeline.</summary>
    public int TargetFramesPerSecond
    {
        get => Volatile.Read(ref _targetFramesPerSecond);
        set => Volatile.Write(ref _targetFramesPerSecond, Math.Clamp(value, 1, 120));
    }

    public void RequestAccess()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        CancellationToken cancellationToken;
        lock (_gate)
        {
            TearDownSession();

            _requestCancellation = new CancellationTokenSource();
            cancellationToken = _requestCancellation.Token;
        }

        SetState(ScreenCaptureAccessState.Requesting, null);
        _ = RequestAccessAsync(cancellationToken);
    }

    /// <summary>Closes the session and stops capturing. Safe to call when nothing is running.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            TearDownSession();
        }

        SetState(ScreenCaptureAccessState.Inactive, null);
    }

    /// <summary>
    /// Copies the requested desktop region as 8-bit grayscale. The region is expressed in the same
    /// global coordinates the overlay windows use.
    /// </summary>
    public bool TryCaptureRegion(int x, int y, int width, int height, byte[] buffer)
    {
        if (width <= 0 || height <= 0 || buffer.Length < width * height)
        {
            return false;
        }

        PipeWireFrameReader? reader;
        uint nodeId;

        lock (_gate)
        {
            if (_session is null)
            {
                return false;
            }

            if (BuildCropRequest(x, y, width, height) is not { } crop)
            {
                return false;
            }

            nodeId = crop.NodeId;
            reader = GetOrStartReader(crop);
        }

        if (reader is null)
        {
            return false;
        }

        if (reader.FailureReason is { } failure)
        {
            lock (_gate)
            {
                DisposeReader(nodeId);
                _brokenNodes.Add(nodeId);
            }

            SetState(ScreenCaptureAccessState.Failed, failure);
            return false;
        }

        return reader.TryCopyLatestFrame(buffer);
    }

    private async Task RequestAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            var session = await ScreenCastPortalClient.StartAsync(allowMultipleMonitors: true, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                session.Dispose();
                return;
            }

            session.Closed += OnSessionClosed;

            lock (_gate)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    session.Closed -= OnSessionClosed;
                    session.Dispose();
                    return;
                }

                _session = session;
                _brokenNodes.Clear();
                _warnings.Clear();
            }

            foreach (var stream in session.Streams)
            {
                Trace.WriteLine($"ScreenCast stream: node={stream.NodeId} mapping_id='{stream.MappingId}' size={stream.Size}");
            }

            SetState(ScreenCaptureAccessState.Active, null);
        }
        catch (OperationCanceledException)
        {
            // A newer request (or Stop) superseded this one.
        }
        catch (ScreenCastPortalException ex)
        {
            SetState(ex.UserCancelled ? ScreenCaptureAccessState.Denied : ScreenCaptureAccessState.Failed, ex.Message);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Opening a ScreenCast session failed: {ex}");
            SetState(ScreenCaptureAccessState.Failed, ex.Message);
        }
    }

    private void OnSessionClosed(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _session))
            {
                return;
            }

            DisposeAllReaders();
            _session = null;
        }

        SetState(ScreenCaptureAccessState.Failed, "The compositor ended the screen capture session.");
    }

    /// <summary>
    /// Maps a desktop region onto the stream that shows it. The region is in X11 coordinates while
    /// the stream is in the output's own pixels, so it is rescaled by the ratio between the two —
    /// which is a no-op unless XWayland and the compositor disagree about the output's resolution.
    /// </summary>
    private CropRequest? BuildCropRequest(int x, int y, int width, int height)
    {
        var session = _session;
        if (session is null)
        {
            return null;
        }

        var monitors = _monitorProvider();
        var centre = new PixelPoint(x + width / 2, y + height / 2);

        foreach (var stream in session.Streams)
        {
            var bounds = ResolveStreamBounds(stream, monitors);
            if (bounds is not { } monitorBounds || !monitorBounds.Contains(centre))
            {
                WarnOnce($"unmatched:{stream.NodeId}",
                    $"Stream '{stream.MappingId}' ({stream.Size}) does not cover {centre}; " +
                    $"known monitors: {string.Join(", ", monitors.Select(m => $"{m.Name} {m.Bounds}"))}");
                continue;
            }

            var scaleX = (double)stream.Size.Width / monitorBounds.Width;
            var scaleY = (double)stream.Size.Height / monitorBounds.Height;

            var left = (int)Math.Round((x - monitorBounds.X) * scaleX);
            var top = (int)Math.Round((y - monitorBounds.Y) * scaleY);
            var cropWidth = Math.Max(2, (int)Math.Round(width * scaleX));
            var cropHeight = Math.Max(2, (int)Math.Round(height * scaleY));

            // Keep the crop inside the frame; videocrop rejects negative or oversized margins.
            left = Math.Clamp(left, 0, Math.Max(0, stream.Size.Width - cropWidth));
            top = Math.Clamp(top, 0, Math.Max(0, stream.Size.Height - cropHeight));
            cropWidth = Math.Min(cropWidth, stream.Size.Width - left);
            cropHeight = Math.Min(cropHeight, stream.Size.Height - top);

            if (cropWidth <= 0 || cropHeight <= 0)
            {
                return null;
            }

            // The pipeline rescales the crop back to the requested size, so a frame always fills
            // exactly width * height bytes regardless of the monitor's scale factor.
            return new CropRequest(
                stream.NodeId,
                left,
                top,
                stream.Size.Width - left - cropWidth,
                stream.Size.Height - top - cropHeight,
                width,
                height,
                TargetFramesPerSecond);
        }

        return null;
    }

    /// <summary>
    /// Works out where on the desktop a stream's monitor sits. The output name is authoritative;
    /// matching by resolution is a fallback for portal backends that omit <c>mapping_id</c>, and it
    /// only helps when the monitor it picks is unambiguous.
    /// </summary>
    private static PixelRect? ResolveStreamBounds(ScreenCastStream stream, IReadOnlyList<LinuxMonitor> monitors)
    {
        if (stream.MappingId.Length > 0)
        {
            foreach (var monitor in monitors)
            {
                if (string.Equals(monitor.Name, stream.MappingId, StringComparison.Ordinal))
                {
                    return monitor.Bounds;
                }
            }
        }

        var bySize = monitors
            .Where(monitor => monitor.Bounds.Size == stream.Size)
            .Select(monitor => (PixelRect?)monitor.Bounds)
            .ToList();

        return bySize.Count == 1 ? bySize[0] : null;
    }

    /// <summary>
    /// Returns the reader for the requested crop, starting or restarting the pipeline when the crop
    /// (or the frame rate) changed. Callers must hold <see cref="_gate"/>.
    /// </summary>
    private PipeWireFrameReader? GetOrStartReader(CropRequest crop)
    {
        if (_brokenNodes.Contains(crop.NodeId))
        {
            return null;
        }

        if (_readers.TryGetValue(crop.NodeId, out var active))
        {
            if (active.Crop == crop)
            {
                return active.Reader;
            }

            DisposeReader(crop.NodeId);
        }

        var reader = PipeWireFrameReader.TryStart(crop.NodeId, crop.Left, crop.Top, crop.Right, crop.Bottom,
            crop.OutputWidth, crop.OutputHeight, crop.FramesPerSecond);

        if (reader is null)
        {
            SetState(ScreenCaptureAccessState.Failed,
                "Could not start gst-launch-1.0. Install GStreamer with the PipeWire plugin.");
            return null;
        }

        _readers[crop.NodeId] = new ActiveReader(crop, reader);
        return reader;
    }

    private void DisposeReader(uint nodeId)
    {
        if (_readers.Remove(nodeId, out var active))
        {
            active.Reader.Dispose();
        }
    }

    private void DisposeAllReaders()
    {
        foreach (var active in _readers.Values)
        {
            active.Reader.Dispose();
        }

        _readers.Clear();
        _brokenNodes.Clear();
    }

    private void TearDownSession()
    {
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;

        DisposeAllReaders();

        if (_session is not null)
        {
            _session.Closed -= OnSessionClosed;
            _session.Dispose();
            _session = null;
        }
    }

    /// <summary>Keeps per-frame diagnostics from flooding the log.</summary>
    private void WarnOnce(string key, string message)
    {
        if (_warnings.Add(key))
        {
            Trace.WriteLine(message);
        }
    }

    private void SetState(ScreenCaptureAccessState state, string? detail)
    {
        Trace.WriteLine($"Screen capture state: {state}{(detail is null ? string.Empty : $" ({detail})")}");

        if (State == state && StateDetail == detail)
        {
            return;
        }

        State = state;
        StateDetail = detail;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            TearDownSession();
        }
    }

    /// <summary>
    /// The pipeline configuration derived from a capture request. Crop values are the margins
    /// videocrop trims from each edge of the stream.
    /// </summary>
    private readonly record struct CropRequest(
        uint NodeId,
        int Left,
        int Top,
        int Right,
        int Bottom,
        int OutputWidth,
        int OutputHeight,
        int FramesPerSecond);

    private readonly record struct ActiveReader(CropRequest Crop, PipeWireFrameReader Reader);
}
