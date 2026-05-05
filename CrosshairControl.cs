using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CrosshairOverlay;

public sealed class CrosshairControl : Control
{
    private static readonly ISolidColorBrush[] PreviewBrushes = CreatePreviewBrushes();

    private OverlaySettings _settings = new();
    private double _gridOffsetX;
    private double _gridOffsetY;
    private byte[]? _motionPreviewBuffer;
    private int _motionPreviewWidth;
    private int _motionPreviewHeight;

    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;
        InvalidateVisual();
    }

    /// <summary>
    /// Sets the accumulated motion-compensation offset for the dot grid and redraws.
    /// The lattice wraps periodically so no clamping is needed.
    /// </summary>
    public void SetGridOffset(double offsetX, double offsetY)
    {
        _gridOffsetX = offsetX;
        _gridOffsetY = offsetY;
        InvalidateVisual();
    }

    public void SetMotionCapturePreview(byte[]? buffer, int width, int height)
    {
        if (buffer == null || width <= 0 || height <= 0)
        {
            _motionPreviewBuffer = null;
            _motionPreviewWidth = 0;
            _motionPreviewHeight = 0;
            InvalidateVisual();
            return;
        }

        _motionPreviewBuffer = new byte[buffer.Length];
        Buffer.BlockCopy(buffer, 0, _motionPreviewBuffer, 0, buffer.Length);
        _motionPreviewWidth = width;
        _motionPreviewHeight = height;
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
        DrawMotionRegionPreview(context, cx, cy);
        DrawMotionCapturePreview(context);
    }

    private void DrawMotionCapturePreview(DrawingContext context)
    {
        if (!_settings.EnableMotionDetection || !_settings.DebugShowMotionCapturePreview || _motionPreviewBuffer == null || _motionPreviewWidth <= 0 || _motionPreviewHeight <= 0)
        {
            return;
        }

        const double margin = 16;
        const double maxPreviewSize = 160;
        const double padding = 6;
        var scale = Math.Min(maxPreviewSize / _motionPreviewWidth, maxPreviewSize / _motionPreviewHeight);
        if (scale <= 0)
        {
            return;
        }

        var sampleStep = Math.Max(1, Math.Max(_motionPreviewWidth, _motionPreviewHeight) / 96);
        var cellSize = scale * sampleStep;
        var previewWidth = _motionPreviewWidth * scale;
        var previewHeight = _motionPreviewHeight * scale;
        var panelRect = new Rect(
            margin,
            Math.Max(margin, Bounds.Height - previewHeight - (padding * 2) - margin),
            previewWidth + (padding * 2),
            previewHeight + (padding * 2));

        context.DrawRectangle(
            new SolidColorBrush(new Color(190, 14, 18, 24)),
            new Pen(new SolidColorBrush(new Color(220, 0, 120, 212)), 1),
            panelRect);

        var pixelOriginX = panelRect.X + padding;
        var pixelOriginY = panelRect.Y + padding;

        for (var y = 0; y < _motionPreviewHeight; y += sampleStep)
        {
            var drawY = pixelOriginY + (y * scale);
            for (var x = 0; x < _motionPreviewWidth; x += sampleStep)
            {
                var intensity = _motionPreviewBuffer[(y * _motionPreviewWidth) + x];
                context.DrawRectangle(
                    PreviewBrushes[intensity],
                    null,
                    new Rect(pixelOriginX + (x * scale), drawY, cellSize + 0.5, cellSize + 0.5));
            }
        }
    }

    private void DrawMotionRegionPreview(DrawingContext context, double cx, double cy)
    {
        if (!_settings.MotionRegionPreview)
        {
            return;
        }

        var rs = Math.Max(8, _settings.MotionRegionSize);
        var halfRs = rs / 2.0;
        var fillBrush = new SolidColorBrush(new Color(25, 255, 220, 0));
        var borderPen = new Pen(new SolidColorBrush(new Color(200, 255, 220, 0)), 1.5);
        context.DrawRectangle(fillBrush, borderPen, new Rect(cx - halfRs, cy - halfRs, rs, rs));
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

        // Motion-compensation offset: reduce to [0, spacing) for periodic wrapping.
        // The infinite lattice has the same visual appearance for any multiple of spacing,
        // so we only need the fractional part to determine which dots are visible.
        var ox = ((_gridOffsetX % spacing) + spacing) % spacing;
        var oy = ((_gridOffsetY % spacing) + spacing) % spacing;

        if (string.Equals(areaShape, "Circle", StringComparison.OrdinalIgnoreCase))
        {
            var radiusPoints = Math.Max(1, _settings.DotGridRadiusPoints);
            // Extend iteration by 1 extra cell in each direction so dots entering
            // from the opposite side are always included during wrapping.
            for (var j = -(radiusPoints + 1); j <= radiusPoints + 1; j++)
            {
                for (var i = -(radiusPoints + 1); i <= radiusPoints + 1; i++)
                {
                    var dotRelX = ox + i * spacing;
                    var dotRelY = oy + j * spacing;
                    var radiusPx = radiusPoints * spacing;
                    if ((dotRelX * dotRelX) + (dotRelY * dotRelY) > (radiusPx * radiusPx))
                    {
                        continue;
                    }

                    DrawPoint(context, brush, cx + dotRelX, cy + dotRelY, pointSize, _settings.DotGridPointShape);
                }
            }

            return;
        }

        // Rectangle area — dots fill a (columns × rows) grid that wraps periodically.
        var rows = Math.Max(1, _settings.DotGridRows);
        var columns = Math.Max(1, _settings.DotGridColumns);
        var halfW = (columns - 1) * spacing / 2.0;
        var halfH = (rows - 1) * spacing / 2.0;

        // Iterate one extra column/row beyond the normal range so that the incoming
        // dot from the opposite side is always present during the wrap transition.
        var colRange = columns / 2 + 2;
        var rowRange = rows / 2 + 2;

        for (var j = -rowRange; j <= rowRange; j++)
        {
            var dotRelY = oy + j * spacing;
            if (Math.Abs(dotRelY) > halfH + 0.5)
            {
                continue;
            }

            for (var i = -colRange; i <= colRange; i++)
            {
                var dotRelX = ox + i * spacing;
                if (Math.Abs(dotRelX) > halfW + 0.5)
                {
                    continue;
                }

                DrawPoint(context, brush, cx + dotRelX, cy + dotRelY, pointSize, _settings.DotGridPointShape);
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

    private static ISolidColorBrush[] CreatePreviewBrushes()
    {
        var brushes = new ISolidColorBrush[256];
        for (var i = 0; i < brushes.Length; i++)
        {
            brushes[i] = new SolidColorBrush(new Color(255, (byte)i, (byte)i, (byte)i));
        }

        return brushes;
    }
}
