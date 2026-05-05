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
            var d = new OverlaySettings();

            s.EnableCrosshair = true;
            s.EnableDotGrid = false;
            s.EnableCenterDot = false;

            s.CrosshairColor = d.CrosshairColor;
            s.CrosshairOpacity = d.CrosshairOpacity;
            s.CrosshairHorizontalLength = d.CrosshairHorizontalLength;
            s.CrosshairVerticalLength = d.CrosshairVerticalLength;
            s.CrosshairGap = d.CrosshairGap;
            s.CrosshairThickness = d.CrosshairThickness;
        }),

        new OverlayPreset("default-dotgrid", "Preset_default-dotgrid", s =>
        {
            var d = new OverlaySettings();

            s.EnableDotGrid = true;
            s.EnableCrosshair = false;
            s.EnableCenterDot = false;

            s.DotGridPointSize = d.DotGridPointSize;
            s.DotGridPointShape = d.DotGridPointShape;
            s.DotGridColor = d.DotGridColor;
            s.DotGridOpacity = d.DotGridOpacity;
            s.DotGridAreaShape = d.DotGridAreaShape;
            s.DotGridRows = d.DotGridRows;
            s.DotGridColumns = d.DotGridColumns;
            s.DotGridRadiusPoints = d.DotGridRadiusPoints;
            s.DotGridSpacing = d.DotGridSpacing;
        }),

        new OverlayPreset("default-centerdot", "Preset_default-centerdot", s =>
        {
            var d = new OverlaySettings();

            s.EnableCenterDot = true;
            s.EnableCrosshair = false;
            s.EnableDotGrid = false;

            s.CenterDotSize = d.CenterDotSize;
            s.CenterDotShape = d.CenterDotShape;
            s.CenterDotColor = d.CenterDotColor;
            s.CenterDotOpacity = d.CenterDotOpacity;
        }),

        new OverlayPreset("grid-sniper", "Preset_grid-sniper", s =>
        {
            s.EnableCenterDot = true;
            s.CenterDotShape = "Circle";
            s.CenterDotColor = "#FFFFFF";
            s.CenterDotSize = 6;
            s.CenterDotOpacity = 1.0;

            s.EnableDotGrid = true;
            s.DotGridPointShape = "Square";
            s.DotGridAreaShape = "Square";
            s.DotGridPointSize = 4;
            s.DotGridSpacing = 40;
            s.DotGridRows = 7;
            s.DotGridColumns = 15;
            s.DotGridOpacity = 0.7;
            s.DotGridColor = "#FFFFFF";

            s.EnableCrosshair = true;
            s.CrosshairColor = "#FFFFFF";
            s.CrosshairOpacity = 0.5;
            s.CrosshairHorizontalLength = 300;
            s.CrosshairVerticalLength = 0;
            s.CrosshairGap = 10;
            s.CrosshairThickness = 2;
        }),
    ];
}
