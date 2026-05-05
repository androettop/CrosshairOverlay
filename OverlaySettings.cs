using System.Collections.Generic;

namespace CrosshairOverlay;

public sealed class OverlaySettings
{
    public bool EnableCenterDot { get; set; } = false;

    public double CenterDotSize { get; set; } = 8;

    public string CenterDotShape { get; set; } = "Circle";

    public string CenterDotColor { get; set; } = "#FFFFFF";

    public double CenterDotOpacity { get; set; } = 1.0;

    public bool EnableDotGrid { get; set; } = false;

    public double DotGridPointSize { get; set; } = 3;

    public string DotGridPointShape { get; set; } = "Circle";

    public string DotGridColor { get; set; } = "#FFFFFF";

    public double DotGridOpacity { get; set; } = 0.6;

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

    public int MotionRegionSize { get; set; } = 200;

    public bool MotionRegionPreview { get; set; } = false;

    public int MotionSmoothingFrames { get; set; } = 5;

    public double MotionCancellationIntensity { get; set; } = 1.0;

    public int MotionCaptureFps { get; set; } = 30;

    public double MotionDeadZonePixels { get; set; } = 0.5;

    public int MotionMonitorIndex { get; set; } = 0;
}
