using System;

namespace CrosshairOverlay.Platform;

public enum ScreenCaptureAccessState
{
    /// <summary>The platform captures the screen without asking, so there is nothing to manage.</summary>
    NotRequired,

    /// <summary>Motion detection is off, so no capture session is open.</summary>
    Inactive,

    /// <summary>Waiting for the user to answer the compositor's permission prompt.</summary>
    Requesting,

    /// <summary>Frames are arriving.</summary>
    Active,

    /// <summary>The user dismissed or denied the permission prompt.</summary>
    Denied,

    /// <summary>The session was granted but later broke, or could not be set up.</summary>
    Failed,
}

/// <summary>
/// Surfaces the state of a permission-gated screen capture session so the settings window can
/// explain what is going on and offer a way to ask again.
/// </summary>
public interface IScreenCaptureAccess
{
    /// <summary>True when the platform gates screen capture behind an explicit permission prompt.</summary>
    bool RequiresPermission { get; }

    ScreenCaptureAccessState State { get; }

    /// <summary>Extra context for <see cref="ScreenCaptureAccessState.Failed"/>, otherwise null.</summary>
    string? StateDetail { get; }

    event EventHandler? StateChanged;

    /// <summary>Tears down any existing session and asks the compositor for permission again.</summary>
    void RequestAccess();
}
