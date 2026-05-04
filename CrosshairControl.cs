using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CrosshairOverlay;

public sealed class CrosshairControl : Control
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<CrosshairControl, Color>(nameof(Color), Colors.Red);

    public static readonly StyledProperty<double> OpacityLevelProperty =
        AvaloniaProperty.Register<CrosshairControl, double>(nameof(OpacityLevel), 0.9);

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<CrosshairControl, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CrosshairControl, double>(nameof(Gap), 4);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<CrosshairControl, double>(nameof(Thickness), 2);

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double OpacityLevel
    {
        get => GetValue(OpacityLevelProperty);
        set => SetValue(OpacityLevelProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Max(0, Size);
        var gap = Math.Max(0, Gap);
        var thickness = Math.Max(1, Thickness);
        var opacity = Math.Clamp(OpacityLevel, 0, 1);

        if (size <= 0)
        {
            return;
        }

        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;

        var alpha = (byte)Math.Clamp(Math.Round(Color.A * opacity), 0, 255);
        var color = new Color(alpha, Color.R, Color.G, Color.B);
        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, thickness);

        var leftStart = new Point(cx - gap - size, cy);
        var leftEnd = new Point(cx - gap, cy);
        context.DrawLine(pen, leftStart, leftEnd);

        var rightStart = new Point(cx + gap, cy);
        var rightEnd = new Point(cx + gap + size, cy);
        context.DrawLine(pen, rightStart, rightEnd);

        var topStart = new Point(cx, cy - gap - size);
        var topEnd = new Point(cx, cy - gap);
        context.DrawLine(pen, topStart, topEnd);

        var bottomStart = new Point(cx, cy + gap);
        var bottomEnd = new Point(cx, cy + gap + size);
        context.DrawLine(pen, bottomStart, bottomEnd);
    }
}
