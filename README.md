# Crosshair Overlay

An always-on-top transparent crosshair for Windows, macOS and Linux that helps **reduce motion sickness** by giving your eyes a fixed reference point on screen.

> A static anchor at the center of the screen helps your brain reconcile on-screen motion, reducing nausea and disorientation caused by games without a built-in crosshair.

## Features

- Center dot, classic crosshair, and dot grid — fully customizable
- Click-through overlay (never blocks mouse input)
- Windows, macOS and Linux (X11 and Wayland via XWayland)
- Multi-monitor support
- Live settings panel via system tray icon
- Settings saved automatically

## Download & Install

Go to the [**latest release**](https://github.com/androettop/CrosshairOverlay/releases/latest) and download the installer for your platform:

| Platform | File | Notes |
|---|---|---|
| Windows 10/11 | `CrosshairOverlay-win-x64-setup.exe` | Run the installer — no extra dependencies needed |
| macOS (Intel) | `CrosshairOverlay-osx-x64.dmg` | Open the DMG and drag the app to Applications |
| macOS (Apple Silicon) | `CrosshairOverlay-osx-arm64.dmg` | Open the DMG and drag the app to Applications |
| Linux x64 | `CrosshairOverlay-linux-x64.tar.gz` | Extract and run the binary — see [Linux notes](#linux-x11-and-wayland) |

> **macOS:** On first launch, macOS may show a security warning. Go to **System Settings → Privacy & Security** and click *Open Anyway*.

## Linux (X11 and Wayland)

The app renders through X11. On a Wayland session it runs under XWayland, which is enough for the
overlay itself: the window is made click-through with an empty XShape input region and marked
override-redirect, so compositors treat it as unmanaged and keep it out of alt-tab and the taskbar.

Motion detection is different, because Wayland never hands screen content to a client without
consent:

- Turning **motion detection** on opens an `xdg-desktop-portal` screen sharing prompt. Pick the
  monitor your overlay is on. Turning the option off closes the session, and turning it back on
  asks again — no permission token is stored.
- **Settings → Motion → Screen capture access** shows whether frames are arriving and lets you ask
  again if you dismissed the prompt or the compositor ended the session.
- Requires `xdg-desktop-portal` with a backend for your compositor, plus GStreamer with the
  PipeWire plugin (`gst-plugin-pipewire`, `gst-plugins-base`, `gst-plugins-good`), which is what
  reads the capture stream.

Two limitations are inherent to Wayland rather than to this app:

- The overlay cannot exclude *itself* from the capture stream — there is no Wayland protocol for a
  client to ask for that, so the *Allow capturing overlays* debug switch has no effect here. The
  crosshair is static and adds no motion, but it does sit in the frame motion detection analyses.
  Some compositors can exclude it by rule; on Hyprland that is `no_screen_share` in a window rule
  matching the overlay.
- Compositor effects apply to the overlay like any other window. On Hyprland, for example, blur is
  only disabled with a rule of your own: `windowrule = noblur, class:^(CrosshairOverlay)$`.

## Usage

Run `CrosshairOverlay.exe` (Windows) or open `CrosshairOverlay.app` (macOS). Right-click the tray icon to open Settings.

Press `Esc` to close the overlay.

## Build from Source

```bash
git clone https://github.com/androettop/CrosshairOverlay.git
cd CrosshairOverlay
dotnet run
```

## License

MIT

