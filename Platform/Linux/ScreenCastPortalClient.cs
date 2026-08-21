using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Tmds.DBus.Protocol;

namespace CrosshairOverlay.Platform.Linux;

/// <summary>
/// A single monitor stream handed out by the portal. <paramref name="MappingId"/> is the
/// compositor's name for the output the stream shows; it is how a stream gets matched back to a
/// monitor, because the <c>position</c> property is reported relative to the stream itself on some
/// portal backends (xdg-desktop-portal-hyprland reports 0,0 for every monitor).
/// </summary>
internal sealed record ScreenCastStream(uint NodeId, string MappingId, PixelSize Size);

/// <summary>Raised when the portal refuses or aborts a ScreenCast request.</summary>
internal sealed class ScreenCastPortalException(string message, bool userCancelled = false)
    : Exception(message)
{
    public bool UserCancelled { get; } = userCancelled;
}

/// <summary>
/// An open <c>org.freedesktop.portal.ScreenCast</c> session. Holds the D-Bus connection for as
/// long as the session is alive: closing the connection also tears down the PipeWire nodes.
/// </summary>
internal sealed class ScreenCastSession : IDisposable
{
    private readonly DBusConnection _connection;
    private readonly IDisposable? _closedWatcher;
    private readonly string _sessionHandle;
    private int _disposed;

    internal ScreenCastSession(DBusConnection connection, string sessionHandle,
        IReadOnlyList<ScreenCastStream> streams, IDisposable? closedWatcher)
    {
        _connection = connection;
        _sessionHandle = sessionHandle;
        _closedWatcher = closedWatcher;
        Streams = streams;
    }

    public IReadOnlyList<ScreenCastStream> Streams { get; }

    /// <summary>Raised when the compositor ends the session (for example via "stop sharing").</summary>
    public event EventHandler? Closed;

    internal void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _closedWatcher?.Dispose();
            _connection.TrySendMessage(CreateCloseMessage());
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Closing ScreenCast session failed: {ex}");
        }
        finally
        {
            _connection.Dispose();
        }
    }

    private MessageBuffer CreateCloseMessage()
    {
        using var writer = _connection.GetMessageWriter();
        writer.WriteMethodCallHeader(ScreenCastPortalClient.PortalService, _sessionHandle,
            ScreenCastPortalClient.SessionInterface, "Close", null, MessageFlags.NoReplyExpected);
        return writer.CreateMessage();
    }
}

/// <summary>
/// Drives the CreateSession → SelectSources → Start handshake of
/// <c>org.freedesktop.portal.ScreenCast</c>. Every call to <see cref="StartAsync"/> opens a fresh
/// session, so the compositor asks the user for permission again.
/// </summary>
internal static class ScreenCastPortalClient
{
    internal const string PortalService = "org.freedesktop.portal.Desktop";
    internal const string SessionInterface = "org.freedesktop.portal.Session";

    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string ScreenCastInterface = "org.freedesktop.portal.ScreenCast";
    private const string RequestInterface = "org.freedesktop.portal.Request";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    private const uint SourceTypeMonitor = 1;
    private const uint CursorModeHidden = 1;

    private static int _tokenCounter;

    public static async Task<ScreenCastSession> StartAsync(bool allowMultipleMonitors, CancellationToken cancellationToken)
    {
        var connection = new DBusConnection(DBusAddress.Session
            ?? throw new ScreenCastPortalException("No D-Bus session bus is available."));

        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);

            var senderToken = connection.UniqueName!.Substring(1).Replace('.', '_');
            var version = await GetScreenCastVersionAsync(connection).ConfigureAwait(false);
            var sessionToken = NextToken("session");

            var created = await CallWithRequestAsync(connection, senderToken, "CreateSession", "a{sv}",
                sessionArg: null, parentWindowArg: null,
                handleToken => new Dictionary<string, VariantValue>
                {
                    ["handle_token"] = VariantValue.String(handleToken),
                    ["session_handle_token"] = VariantValue.String(sessionToken),
                }, cancellationToken).ConfigureAwait(false);

            ThrowIfFailed(created.Code, "CreateSession");
            var sessionHandle = created.Results["session_handle"].GetString();

            var selected = await CallWithRequestAsync(connection, senderToken, "SelectSources", "oa{sv}",
                sessionArg: sessionHandle, parentWindowArg: null,
                handleToken =>
                {
                    var options = new Dictionary<string, VariantValue>
                    {
                        ["handle_token"] = VariantValue.String(handleToken),
                        ["types"] = VariantValue.UInt32(SourceTypeMonitor),
                        ["multiple"] = VariantValue.Bool(allowMultipleMonitors),
                    };

                    // cursor_mode was added in ScreenCast v2; hiding the cursor keeps it out of
                    // the motion estimate.
                    if (version >= 2)
                    {
                        options["cursor_mode"] = VariantValue.UInt32(CursorModeHidden);
                    }

                    return options;
                }, cancellationToken).ConfigureAwait(false);

            ThrowIfFailed(selected.Code, "SelectSources");

            var started = await CallWithRequestAsync(connection, senderToken, "Start", "osa{sv}",
                sessionArg: sessionHandle, parentWindowArg: "",
                handleToken => new Dictionary<string, VariantValue>
                {
                    ["handle_token"] = VariantValue.String(handleToken),
                }, cancellationToken).ConfigureAwait(false);

            ThrowIfFailed(started.Code, "Start");

            var streams = ReadStreams(started.Results);
            if (streams.Count == 0)
            {
                throw new ScreenCastPortalException("The portal returned no screen capture streams.");
            }

            ScreenCastSession? session = null;
            var closedWatcher = await connection.AddMatchAsync(
                new MatchRule
                {
                    Type = MessageType.Signal,
                    Sender = PortalService,
                    Path = sessionHandle,
                    Interface = SessionInterface,
                    Member = "Closed",
                },
                (Message _, object? _) => true,
                (Exception? error, bool _, object? _, object? _) =>
                {
                    if (error is null)
                    {
                        session?.RaiseClosed();
                    }
                },
                ObserverFlags.None, null, null, false).ConfigureAwait(false);

            session = new ScreenCastSession(connection, sessionHandle, streams, closedWatcher);
            return session;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void ThrowIfFailed(uint code, string step)
    {
        switch (code)
        {
            case 0:
                return;
            case 1:
                throw new ScreenCastPortalException("Screen capture permission was denied.", userCancelled: true);
            default:
                throw new ScreenCastPortalException($"The portal aborted {step} (response {code}).");
        }
    }

    private static IReadOnlyList<ScreenCastStream> ReadStreams(Dictionary<string, VariantValue> results)
    {
        var streams = new List<ScreenCastStream>();
        if (!results.TryGetValue("streams", out var value) || value.Type != VariantValueType.Array)
        {
            return streams;
        }

        for (var i = 0; i < value.Count; i++)
        {
            var entry = value.GetItem(i);
            var nodeId = entry.GetItem(0).GetUInt32();
            var properties = entry.GetItem(1);

            var mappingId = string.Empty;
            var size = new PixelSize(0, 0);

            for (var j = 0; j < properties.Count; j++)
            {
                var property = properties.GetDictionaryEntry(j);
                switch (property.Key.GetString())
                {
                    case "mapping_id":
                        mappingId = Unwrap(property.Value).GetString();
                        break;
                    case "size":
                        var sizeValue = Unwrap(property.Value);
                        size = new PixelSize(sizeValue.GetItem(0).GetInt32(), sizeValue.GetItem(1).GetInt32());
                        break;
                }
            }

            if (size.Width > 0 && size.Height > 0)
            {
                streams.Add(new ScreenCastStream(nodeId, mappingId, size));
            }
        }

        return streams;
    }

    /// <summary>
    /// Reading an <c>a{sv}</c> already unwraps each value, so the accessors have to be used on the
    /// value itself; this only unwraps again for backends that hand back a nested variant.
    /// </summary>
    private static VariantValue Unwrap(VariantValue value)
        => value.Type == VariantValueType.Variant ? value.GetVariantValue() : value;

    private static string NextToken(string prefix)
        => $"crosshairoverlay_{prefix}_{Interlocked.Increment(ref _tokenCounter)}";

    private static Task<uint> GetScreenCastVersionAsync(DBusConnection connection)
    {
        return connection.CallMethodAsync(CreateVersionMessage(connection),
            (Message message, object? _) => message.GetBodyReader().ReadVariantValue().GetUInt32());
    }

    private static MessageBuffer CreateVersionMessage(DBusConnection connection)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(PortalService, PortalPath, PropertiesInterface, "Get", "ss");
        writer.WriteString(ScreenCastInterface);
        writer.WriteString("version");
        return writer.CreateMessage();
    }

    /// <summary>
    /// Invokes a portal method and awaits the matching <c>Request.Response</c> signal, which is
    /// where the portal reports the outcome once the user has answered the permission prompt.
    /// </summary>
    private static async Task<PortalResponse> CallWithRequestAsync(
        DBusConnection connection,
        string senderToken,
        string member,
        string signature,
        string? sessionArg,
        string? parentWindowArg,
        Func<string, Dictionary<string, VariantValue>> buildOptions,
        CancellationToken cancellationToken)
    {
        var handleToken = NextToken("request");
        var requestPath = $"{PortalPath}/request/{senderToken}/{handleToken}";
        var completion = new TaskCompletionSource<PortalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        var watcher = await connection.AddMatchAsync(
            new MatchRule
            {
                Type = MessageType.Signal,
                Sender = PortalService,
                Path = requestPath,
                Interface = RequestInterface,
                Member = "Response",
            },
            (Message message, object? _) =>
            {
                var reader = message.GetBodyReader();
                return new PortalResponse(reader.ReadUInt32(), reader.ReadDictionaryOfStringToVariantValue());
            },
            (Exception? error, PortalResponse response, object? _, object? _) =>
            {
                if (error is not null)
                {
                    completion.TrySetException(
                        new ScreenCastPortalException($"Lost the portal reply for {member}: {error.Message}"));
                }
                else
                {
                    completion.TrySetResult(response);
                }
            },
            ObserverFlags.EmitOnConnectionDispose, null, null, false).ConfigureAwait(false);

        try
        {
            await connection.CallMethodAsync(
                CreateRequestMessage(connection, member, signature, sessionArg, parentWindowArg, buildOptions(handleToken)),
                (Message message, object? _) => message.GetBodyReader().ReadObjectPathAsString()).ConfigureAwait(false);

            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            watcher.Dispose();
        }
    }

    /// <remarks>
    /// The arguments are written here rather than through a callback: <see cref="MessageWriter"/> is
    /// a mutable struct, so handing it to a delegate would write into a copy and silently drop the
    /// arguments from the message body.
    /// </remarks>
    private static MessageBuffer CreateRequestMessage(
        DBusConnection connection,
        string member,
        string signature,
        string? sessionArg,
        string? parentWindowArg,
        Dictionary<string, VariantValue> options)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(PortalService, PortalPath, ScreenCastInterface, member, signature);

        if (sessionArg is not null)
        {
            writer.WriteObjectPath(sessionArg);
        }

        if (parentWindowArg is not null)
        {
            writer.WriteString(parentWindowArg);
        }

        writer.WriteDictionary(options);
        return writer.CreateMessage();
    }

    private readonly record struct PortalResponse(uint Code, Dictionary<string, VariantValue> Results);
}
