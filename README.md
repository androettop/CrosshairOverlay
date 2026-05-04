# Crosshair Overlay

An always-on-top transparent crosshair for Windows and macOS that helps **reduce motion sickness** by giving your eyes a fixed reference point on screen.

> A static anchor at the center of the screen helps your brain reconcile on-screen motion, reducing nausea and disorientation caused by games without a built-in crosshair.

## Features

- Center dot, classic crosshair, and dot grid — fully customizable
- Click-through overlay (never blocks mouse input)
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
| Linux x64 | `CrosshairOverlay-linux-x64.tar.gz` | Extract and run the binary — **untested, not guaranteed to work** |

> **macOS:** On first launch, macOS may show a security warning. Go to **System Settings → Privacy & Security** and click *Open Anyway*.

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
