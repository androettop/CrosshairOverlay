using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CrosshairOverlay;

public sealed class CrosshairControl : Control
{
    private OverlaySettings _settings = new();

    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;

        DrawDotGrid(context, cx, cy);
        DrawCrosshair(context, cx, cy);
        DrawCenterDot(context, cx, cy);
    }

    private void DrawCenterDot(DrawingContext context, double cx, double cy)
    {
        if (!_settings.EnableCenterDot)
        {
            return;
        }

        var size = Math.Max(0, _settings.CenterDotSize);
        if (size <= 0)
        {
            return;
        }

        var brush = new SolidColorBrush(ParseColor(_settings.CenterDotColor, Colors.White, _settings.CenterDotOpacity));
        DrawPoint(context, brush, cx, cy, size, _settings.CenterDotShape);
    }

    private void DrawDotGrid(DrawingContext context, double cx, double cy)
    {
        if (!_settings.EnableDotGrid)
        {
            return;
        }

        var pointSize = Math.Max(0, _settings.DotGridPointSize);
        if (pointSize <= 0)
        {
            return;
        }

        var spacing = Math.Max(1, _settings.DotGridSpacing);
        var brush = new SolidColorBrush(ParseColor(_settings.DotGridColor, Colors.Lime, _settings.DotGridOpacity));
        var areaShape = _settings.DotGridAreaShape;

        if (string.Equals(areaShape, "Circle", StringComparison.OrdinalIgnoreCase))
        {
            var radiusPoints = Math.Max(1, _settings.DotGridRadiusPoints);
            for (var y = -radiusPoints; y <= radiusPoints; y++)
            {
                for (var x = -radiusPoints; x <= radiusPoints; x++)
                {
                    if ((x * x) + (y * y) > (radiusPoints * radiusPoints))
                    {
                        continue;
                    }

                    DrawPoint(context, brush, cx + (x * spacing), cy + (y * spacing), pointSize, _settings.DotGridPointShape);
                }
            }

            return;
        }

        var rows = Math.Max(1, _settings.DotGridRows);
        var columns = Math.Max(1, _settings.DotGridColumns);
        var startX = cx - ((columns - 1) * spacing / 2);
        var startY = cy - ((rows - 1) * spacing / 2);

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                var x = startX + (col * spacing);
                var y = startY + (row * spacing);
                DrawPoint(context, brush, x, y, pointSize, _settings.DotGridPointShape);
            }
        }
    }

    private void DrawCrosshair(DrawingContext context, double cx, double cy)
    {
        if (!_settings.EnableCrosshair)
        {
            return;
        }

        var hLength = Math.Max(0, _settings.CrosshairHorizontalLength);
        var vLength = Math.Max(0, _settings.CrosshairVerticalLength);
        var gap = Math.Max(0, _settings.CrosshairGap);
        var thickness = Math.Max(1, _settings.CrosshairThickness);

        if (hLength <= 0 && vLength <= 0)
        {
            return;
        }

        var color = ParseColor(_settings.CrosshairColor, Colors.Red, _settings.CrosshairOpacity);
        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, thickness);

        var leftStart = new Point(cx - gap - hLength, cy);
        var leftEnd = new Point(cx - gap, cy);
        context.DrawLine(pen, leftStart, leftEnd);

        var rightStart = new Point(cx + gap, cy);
        var rightEnd = new Point(cx + gap + hLength, cy);
        context.DrawLine(pen, rightStart, rightEnd);

        var topStart = new Point(cx, cy - gap - vLength);
        var topEnd = new Point(cx, cy - gap);
        context.DrawLine(pen, topStart, topEnd);

        var bottomStart = new Point(cx, cy + gap);
        var bottomEnd = new Point(cx, cy + gap + vLength);
        context.DrawLine(pen, bottomStart, bottomEnd);
    }

    private static void DrawPoint(DrawingContext context, IBrush brush, double x, double y, double size, string shape)
    {
        var half = size / 2;
        if (string.Equals(shape, "Square", StringComparison.OrdinalIgnoreCase))
        {
            context.DrawRectangle(brush, null, new Rect(x - half, y - half, size, size));
            return;
        }

        context.DrawEllipse(brush, null, new Point(x, y), half, half);
    }

    private static Color ParseColor(string? colorText, Color fallback, double opacity)
    {
        var baseColor = Color.TryParse(colorText, out var parsed)
            ? parsed
            : fallback;
        var alpha = (byte)Math.Clamp(Math.Round(baseColor.A * Math.Clamp(opacity, 0, 1)), 0, 255);
        return new Color(alpha, baseColor.R, baseColor.G, baseColor.B);
    }
}
