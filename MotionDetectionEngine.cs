using System;

namespace CrosshairOverlay;

/// <summary>
/// Estimates 2D camera translation from sequential grayscale frame captures
/// using SAD block-matching with parabolic sub-pixel refinement and EMA smoothing.
/// All public methods are thread-safe.
/// </summary>
public sealed class MotionDetectionEngine
{
    private byte[]? _prevFrame;
    private byte[]? _currFrame;
    private int _frameWidth;
    private int _frameHeight;
    private double _smoothedDx;
    private double _smoothedDy;
    private readonly object _lock = new();

    /// <summary>
    /// Stores a new captured grayscale frame. Must be called before <see cref="Estimate"/>.
    /// </summary>
    public void PushFrame(byte[] grayscaleBuffer, int width, int height)
    {
        lock (_lock)
        {
            if (_frameWidth != width || _frameHeight != height)
            {
                _frameWidth = width;
                _frameHeight = height;
                _prevFrame = null;
                _smoothedDx = 0;
                _smoothedDy = 0;
            }

            _prevFrame = _currFrame;
            _currFrame = new byte[width * height];
            Buffer.BlockCopy(grayscaleBuffer, 0, _currFrame, 0, width * height);
        }
    }

    /// <summary>
    /// Estimates the frame-to-frame motion vector (dx, dy) in screen pixels.
    /// Returns (0, 0) until at least two frames have been pushed.
    /// </summary>
    public (double dx, double dy) Estimate(int smoothingFrames, double deadZone)
    {
        lock (_lock)
        {
            if (_prevFrame == null || _currFrame == null)
            {
                return (0, 0);
            }

            if (!HasTrackableDetail(_prevFrame, _currFrame, _frameWidth, _frameHeight))
            {
                _smoothedDx = 0;
                _smoothedDy = 0;
                return (0, 0);
            }

            var (rawDx, rawDy) = BlockMatch(_prevFrame, _currFrame, _frameWidth, _frameHeight);

            // EMA smoothing: alpha = 2 / (N + 1). N=1 → immediate; N=30 → heavy smoothing.
            var alpha = 2.0 / (Math.Max(1, smoothingFrames) + 1);
            _smoothedDx = _smoothedDx * (1.0 - alpha) + rawDx * alpha;
            _smoothedDy = _smoothedDy * (1.0 - alpha) + rawDy * alpha;

            // Dead-zone: suppress sub-threshold noise
            var dx = Math.Abs(_smoothedDx) < deadZone ? 0.0 : _smoothedDx;
            var dy = Math.Abs(_smoothedDy) < deadZone ? 0.0 : _smoothedDy;

            return (dx, dy);
        }
    }

    /// <summary>Resets all state (call on region-size change or when detection is disabled).</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _prevFrame = null;
            _currFrame = null;
            _smoothedDx = 0;
            _smoothedDy = 0;
        }
    }

    private static bool HasTrackableDetail(byte[] prev, byte[] curr, int w, int h)
    {
        var tw = Math.Max(8, w * 3 / 5);
        var th = Math.Max(8, h * 3 / 5);
        var tx0 = (w - tw) / 2;
        var ty0 = (h - th) / 2;
        var step = Math.Max(1, Math.Max(w, h) / 64);

        long textureEnergy = 0;
        var sampleCount = 0;

        for (var y = ty0; y < ty0 + th - step; y += step)
        {
            var row = y * w;
            var nextRow = (y + step) * w;

            for (var x = tx0; x < tx0 + tw - step; x += step)
            {
                var index = row + x;
                var rightIndex = row + x + step;
                var downIndex = nextRow + x;

                textureEnergy += Math.Abs(curr[index] - curr[rightIndex]);
                textureEnergy += Math.Abs(curr[index] - curr[downIndex]);
                textureEnergy += Math.Abs(prev[index] - prev[rightIndex]);
                textureEnergy += Math.Abs(prev[index] - prev[downIndex]);
                sampleCount += 4;
            }
        }

        if (sampleCount == 0)
        {
            return false;
        }

        // Flat or near-flat patches produce ambiguous SAD minima; require a small
        // average neighbor contrast before trusting motion estimation.
        return (textureEnergy / (double)sampleCount) >= 2.0;
    }

    private static (double dx, double dy) BlockMatch(byte[] prev, byte[] curr, int w, int h)
    {
        // Template: central 60% of the frame
        var tw = Math.Max(8, w * 3 / 5);
        var th = Math.Max(8, h * 3 / 5);
        var tx0 = (w - tw) / 2;
        var ty0 = (h - th) / 2;

        // Adaptive sample step: targets ~40×40 sampled pixels regardless of region size
        var sampleStep = Math.Max(1, Math.Max(w, h) / 64);

        // Search radius in screen pixels (capped at 16)
        var searchRadius = Math.Max(1, Math.Min(16, Math.Min(w, h) / 8));

        var bestSad = long.MaxValue;
        var bestSx = 0;
        var bestSy = 0;

        for (var sy = -searchRadius; sy <= searchRadius; sy++)
        {
            for (var sx = -searchRadius; sx <= searchRadius; sx++)
            {
                var sad = SadAt(prev, curr, w, h, tw, th, tx0, ty0, sampleStep, sx, sy);
                if (sad < bestSad)
                {
                    bestSad = sad;
                    bestSx = sx;
                    bestSy = sy;
                }
            }
        }

        // Parabolic sub-pixel refinement — X axis
        double subX = bestSx;
        if (bestSx > -searchRadius && bestSx < searchRadius)
        {
            var sadL = SadAt(prev, curr, w, h, tw, th, tx0, ty0, sampleStep, bestSx - 1, bestSy);
            var sadR = SadAt(prev, curr, w, h, tw, th, tx0, ty0, sampleStep, bestSx + 1, bestSy);
            var denom = (double)(sadL + sadR - 2 * bestSad);
            if (denom > 0)
            {
                subX = bestSx - (sadR - sadL) / (2.0 * denom);
            }
        }

        // Parabolic sub-pixel refinement — Y axis
        double subY = bestSy;
        if (bestSy > -searchRadius && bestSy < searchRadius)
        {
            var sadA = SadAt(prev, curr, w, h, tw, th, tx0, ty0, sampleStep, bestSx, bestSy - 1);
            var sadB = SadAt(prev, curr, w, h, tw, th, tx0, ty0, sampleStep, bestSx, bestSy + 1);
            var denom = (double)(sadA + sadB - 2 * bestSad);
            if (denom > 0)
            {
                subY = bestSy - (sadB - sadA) / (2.0 * denom);
            }
        }

        return (subX, subY);
    }

    private static long SadAt(
        byte[] prev, byte[] curr,
        int w, int h,
        int tw, int th, int tx0, int ty0,
        int step, int sx, int sy)
    {
        long sad = 0;
        for (var ty = ty0; ty < ty0 + th; ty += step)
        {
            var ry = ty + sy;
            if (ry < 0 || ry >= h)
            {
                return long.MaxValue / 2;
            }

            var rowPrev = ty * w;
            var rowCurr = ry * w;
            for (var tx = tx0; tx < tx0 + tw; tx += step)
            {
                var rx = tx + sx;
                if (rx < 0 || rx >= w)
                {
                    return long.MaxValue / 2;
                }

                sad += Math.Abs(prev[rowPrev + tx] - curr[rowCurr + rx]);
            }
        }

        return sad;
    }
}
