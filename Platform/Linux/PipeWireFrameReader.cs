using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace CrosshairOverlay.Platform.Linux;

/// <summary>
/// Pulls 8-bit grayscale frames out of a PipeWire node published by the ScreenCast portal.
///
/// The stream is consumed by a long-lived <c>gst-launch-1.0</c> process: GStreamer already knows
/// how to negotiate the node's format (including DMA-buf), so the app only has to read fixed-size
/// frames from its stdout. Cropping and rate limiting happen inside the pipeline, which keeps the
/// data crossing the pipe down to just the region motion detection cares about.
/// </summary>
internal sealed class PipeWireFrameReader : IDisposable
{
    private readonly object _frameLock = new();
    private readonly Process _process;
    private readonly Thread _readerThread;
    private readonly byte[] _frontBuffer;
    private readonly byte[] _readBuffer;
    private readonly int _stride;
    private volatile bool _hasFrame;
    private volatile string? _failureReason;
    private int _disposed;

    private PipeWireFrameReader(Process process, int width, int height, int stride)
    {
        _process = process;
        Width = width;
        Height = height;
        _stride = stride;
        _frontBuffer = new byte[width * height];
        _readBuffer = new byte[stride * height];

        _readerThread = new Thread(ReadFrames)
        {
            IsBackground = true,
            Name = "PipeWire frame reader",
        };
        _readerThread.Start();
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Set once the pipeline stops producing frames; the UI turns this into a reconnect prompt.</summary>
    public string? FailureReason => _failureReason;

    /// <param name="width">Output width in pixels; the crop is rescaled to it so callers always
    /// receive frames of the size they asked for, even on a scaled monitor.</param>
    public static PipeWireFrameReader? TryStart(uint nodeId, int cropLeft, int cropTop, int cropRight, int cropBottom,
        int width, int height, int framesPerSecond)
    {
        // GStreamer pads GRAY8 rows out to a multiple of 4 bytes.
        var stride = (width + 3) & ~3;

        var arguments = string.Join(' ',
            "-q",
            $"pipewiresrc path={nodeId.ToString(CultureInfo.InvariantCulture)}",
            "! videoconvert",
            $"! videocrop top={cropTop} left={cropLeft} right={cropRight} bottom={cropBottom}",
            "! videoscale add-borders=false",
            "! videorate",
            $"! video/x-raw,format=GRAY8,width={width},height={height},framerate={framesPerSecond}/1",
            "! fdsink fd=1");

        var startInfo = new ProcessStartInfo("gst-launch-1.0")
        {
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            // Draining stderr keeps GStreamer from blocking on a full pipe.
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Trace.WriteLine($"gst-launch: {e.Data}");
                }
            };
            process.BeginErrorReadLine();

            return new PipeWireFrameReader(process, width, height, stride);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Starting gst-launch-1.0 failed: {ex}");
            return null;
        }
    }

    /// <summary>Copies the most recent frame; returns false while no frame has arrived yet.</summary>
    public bool TryCopyLatestFrame(byte[] destination)
    {
        if (!_hasFrame || destination.Length < _frontBuffer.Length)
        {
            return false;
        }

        lock (_frameLock)
        {
            if (!_hasFrame)
            {
                return false;
            }

            Array.Copy(_frontBuffer, destination, _frontBuffer.Length);
            return true;
        }
    }

    private void ReadFrames()
    {
        try
        {
            var stream = _process.StandardOutput.BaseStream;
            while (Volatile.Read(ref _disposed) == 0)
            {
                if (!TryReadExactly(stream, _readBuffer))
                {
                    break;
                }

                lock (_frameLock)
                {
                    if (_stride == Width)
                    {
                        Array.Copy(_readBuffer, _frontBuffer, _frontBuffer.Length);
                    }
                    else
                    {
                        for (var row = 0; row < Height; row++)
                        {
                            Array.Copy(_readBuffer, row * _stride, _frontBuffer, row * Width, Width);
                        }
                    }

                    _hasFrame = true;
                }
            }
        }
        catch (Exception ex) when (Volatile.Read(ref _disposed) == 0)
        {
            Trace.WriteLine($"PipeWire frame reader stopped: {ex}");
        }

        if (Volatile.Read(ref _disposed) == 0)
        {
            _hasFrame = false;
            _failureReason ??= "The capture stream ended.";
        }
    }

    private static bool TryReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            _process.WaitForExit(2000);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Stopping gst-launch-1.0 failed: {ex}");
        }
        finally
        {
            _process.Dispose();
        }

        _readerThread.Join(2000);
    }
}
