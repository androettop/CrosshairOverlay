using System;
using System.Collections.Generic;

namespace CrosshairOverlay;

/// <summary>A code-defined preset that applies a partial set of settings overrides.</summary>
public sealed record OverlayPreset(string Id, string NameKey, Action<OverlaySettings> Apply);

public static class OverlayPresets
{
    /// <summary>Scale factor applied to the virtual preview canvas when rendering preset thumbnails.</summary>
    public const double PreviewScale = 0.42;

    /// <summary>Virtual canvas width at which the CrosshairControl renders inside preset previews.</summary>
    public const double BasePreviewWidth = 600;

    /// <summary>Virtual canvas height at which the CrosshairControl renders inside preset previews.</summary>
    public const double BasePreviewHeight = 400;

    /// <summary>
    /// The definitive list of presets shown in the Settings > General > Presets section.
    /// Each preset explicitly sets all three feature-enable flags so it overrides the current
    /// state regardless of whether the default value is on or off.
    /// </summary>
    public static readonly IReadOnlyList<OverlayPreset> All =
    [
        new OverlayPreset("default-crosshair", "Preset_default-crosshair", s =>
        {
            s.EnableCrosshair = true;
            s.EnableDotGrid = false;
            s.EnableCenterDot = false;
        }),

        new OverlayPreset("default-dotgrid", "Preset_default-dotgrid", s =>
        {
            s.EnableDotGrid = true;
            s.EnableCrosshair = false;
            s.EnableCenterDot = false;
        }),

        new OverlayPreset("default-centerdot", "Preset_default-centerdot", s =>
        {
            s.EnableCenterDot = true;
            s.EnableCrosshair = false;
            s.EnableDotGrid = false;
        }),
    ];
}
