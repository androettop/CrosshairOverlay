using System.Collections.Generic;

namespace CrosshairOverlay;

/// <summary>Snapshot of Appearance, Motion and Debug settings captured for a user preset.</summary>
public sealed class PresetValues
{
    // Appearance – Center Dot
    public bool EnableCenterDot { get; set; }
    public double CenterDotSize { get; set; } = 8;
    public string CenterDotShape { get; set; } = "Circle";
    public string CenterDotColor { get; set; } = "#FFFFFF";
    public double CenterDotOpacity { get; set; } = 1.0;

    // Appearance – Dot Grid
    public bool EnableDotGrid { get; set; }
    public double DotGridPointSize { get; set; } = 6;
    public string DotGridPointShape { get; set; } = "Circle";
    public string DotGridColor { get; set; } = "#FFFFFF";
    public double DotGridOpacity { get; set; } = 0.7;
    public string DotGridAreaShape { get; set; } = "Circle";
    public int DotGridRows { get; set; } = 7;
    public int DotGridColumns { get; set; } = 9;
    public int DotGridRadiusPoints { get; set; } = 5;
    public double DotGridSpacing { get; set; } = 40;

    // Appearance – Crosshair
    public bool EnableCrosshair { get; set; }
    public string CrosshairColor { get; set; } = "#FFFFFF";
    public double CrosshairOpacity { get; set; } = 1;
    public double CrosshairHorizontalLength { get; set; } = 20;
    public double CrosshairVerticalLength { get; set; } = 20;
    public double CrosshairGap { get; set; } = 10;
    public double CrosshairThickness { get; set; } = 2;

    // Motion
    public bool EnableMotionDetection { get; set; }
    public int MotionRegionSize { get; set; } = 360;
    public bool MotionRegionPreview { get; set; }
    public int MotionSmoothingFrames { get; set; } = 5;
    public double MotionCancellationIntensity { get; set; } = 0.15;
    public int MotionCaptureFps { get; set; } = 30;
    public double MotionDeadZonePixels { get; set; } = 0.5;

    // Debug
    public bool DebugShowMotionCapturePreview { get; set; }
    public bool DebugAllowConfigWindowCapture { get; set; }
    public bool DebugAllowOverlayCapture { get; set; }
}

/// <summary>A user-defined preset saved from the current configuration.</summary>
public sealed class UserPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public PresetValues Values { get; set; } = new();
}

public sealed class OverlaySettings
{
    public bool EnableCenterDot { get; set; } = false;

    public double CenterDotSize { get; set; } = 8;

    public string CenterDotShape { get; set; } = "Circle";

    public string CenterDotColor { get; set; } = "#FFFFFF";

    public double CenterDotOpacity { get; set; } = 1.0;

    public bool EnableDotGrid { get; set; } = false;

    public double DotGridPointSize { get; set; } = 6;

    public string DotGridPointShape { get; set; } = "Circle";

    public string DotGridColor { get; set; } = "#FFFFFF";

    public double DotGridOpacity { get; set; } = 0.7;

    public string DotGridAreaShape { get; set; } = "Circle";

    public int DotGridRows { get; set; } = 7;

    public int DotGridColumns { get; set; } = 9;

    public int DotGridRadiusPoints { get; set; } = 5;

    public double DotGridSpacing { get; set; } = 40;

    public bool EnableCrosshair { get; set; } = false;

    public string CrosshairColor { get; set; } = "#FFFFFF";

    public double CrosshairOpacity { get; set; } = 1;

    public double CrosshairHorizontalLength { get; set; } = 20;

    public double CrosshairVerticalLength { get; set; } = 20;

    public double CrosshairGap { get; set; } = 10;

    public double CrosshairThickness { get; set; } = 2;

    public List<int> EnabledMonitorIndices { get; set; } = [0];

    public string Language { get; set; } = "";

    public int MonitorIndex { get; set; } = 0;

    public bool EnableMotionDetection { get; set; } = false;

    public int MotionRegionSize { get; set; } = 360;

    public bool MotionRegionPreview { get; set; } = false;

    public int MotionSmoothingFrames { get; set; } = 5;

    public double MotionCancellationIntensity { get; set; } = 0.15;

    public int MotionCaptureFps { get; set; } = 30;

    public double MotionDeadZonePixels { get; set; } = 0.5;

    public bool DebugShowMotionCapturePreview { get; set; } = false;
    public bool DebugAllowConfigWindowCapture { get; set; } = false;
    public bool DebugAllowOverlayCapture { get; set; } = false;
    public bool DebugTabUnlocked { get; set; } = false;

    public List<UserPreset> UserPresets { get; set; } = [];
}
