namespace CrosshairOverlay;

public sealed class OverlaySettings
{
    public string Color { get; set; } = "#FF0000";

    public double Opacity { get; set; } = 0.9;

    public double Size { get; set; } = 16;

    public double Gap { get; set; } = 4;

    public double Thickness { get; set; } = 2;

    public int MonitorIndex { get; set; } = 0;
}
