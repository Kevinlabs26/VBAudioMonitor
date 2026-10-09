using System.ComponentModel;

namespace VBAudioMonitor;

/// <summary>
/// Professional stereo acoustic peak meter with L/R channels, peak hold, and clipping indicator.
/// </summary>
internal sealed class LevelMeter : Control
{
    private float leftLevel;
    private float rightLevel;
    private float leftPeakHold;
    private float rightPeakHold;
    private DateTime lastLeftPeakTime;
    private DateTime lastRightPeakTime;
    private bool isClipped;
    private DateTime lastClipTime;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Level
    {
        get => Math.Max(leftLevel, rightLevel);
        set => SetStereoLevels(value, value);
    }

    public LevelMeter()
    {
        BackColor = Theme.Surface;
        AccessibleName = "立体声双轨电平表";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Surface;
        Invalidate();
    }

    /// <summary>
    /// Sets discrete left and right channel audio peaks with peak-hold tracking.
    /// </summary>
    public void SetStereoLevels(float leftPeakValue, float rightPeakValue)
    {
        var currentTime = DateTime.UtcNow;
        leftLevel = Math.Clamp(leftPeakValue, 0f, 1f);
        rightLevel = Math.Clamp(rightPeakValue, 0f, 1f);

        UpdatePeakHold(ref leftPeakHold, ref lastLeftPeakTime, leftLevel, currentTime);
        UpdatePeakHold(ref rightPeakHold, ref lastRightPeakTime, rightLevel, currentTime);

        if (leftLevel >= 0.995f || rightLevel >= 0.995f)
        {
            isClipped = true;
            lastClipTime = currentTime;
        }
        else if (isClipped && (currentTime - lastClipTime).TotalSeconds > 1.5)
        {
            isClipped = false;
        }

        Invalidate();
    }

    private static void UpdatePeakHold(ref float peakHoldValue, ref DateTime lastPeakTime, float currentLevel, DateTime currentTime)
    {
        if (currentLevel >= peakHoldValue)
        {
            peakHoldValue = currentLevel;
            lastPeakTime = currentTime;
        }
        else if ((currentTime - lastPeakTime).TotalSeconds > 1.0)
        {
            peakHoldValue = Math.Max(0f, peakHoldValue - 0.05f);
        }
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        base.OnPaint(paintEventArgs);
        var graphicsContext = paintEventArgs.Graphics;
        graphicsContext.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int channelLabelWidth = 18;
        int clipIndicatorWidth = 38;
        int meterAvailableWidth = Math.Max(20, Width - channelLabelWidth - clipIndicatorWidth - 8);
        int channelBarHeight = Math.Max(4, (Height - 6) / 2);

        // Draw Channel L
        DrawChannelTrack(graphicsContext, 0, 1, channelLabelWidth, channelBarHeight, meterAvailableWidth, "L", leftLevel, leftPeakHold);

        // Draw Channel R
        DrawChannelTrack(graphicsContext, 0, channelBarHeight + 4, channelLabelWidth, channelBarHeight, meterAvailableWidth, "R", rightLevel, rightPeakHold);

        // Draw Clip Indicator LED
        DrawClipIndicator(graphicsContext, Width - clipIndicatorWidth - 1, 1, clipIndicatorWidth, Height - 3);
    }

    private void DrawChannelTrack(Graphics canvas, int startX, int startY, int labelWidth, int barHeight, int trackWidth, string labelText, float channelValue, float peakValue)
    {
        using var labelFont = Theme.MonoFont(10.5f, true);
        TextRenderer.DrawText(canvas, labelText, labelFont, new Rectangle(startX, startY - 1, labelWidth, barHeight + 2), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        const int segmentCount = 26;
        float segmentStep = trackWidth / (float)segmentCount;
        float segmentWidth = Math.Max(2f, segmentStep - 2.5f);
        double channelDecibels = channelValue > 0.0001f ? 20 * Math.Log10(channelValue) : -60.0;
        int activeSegments = channelValue < 0.0005f ? 0 : (int)Math.Ceiling(Math.Clamp((channelDecibels + 54.0) / 54.0, 0.0, 1.0) * segmentCount);

        double peakDecibels = peakValue > 0.0001f ? 20 * Math.Log10(peakValue) : -60.0;
        int peakSegmentIndex = (int)Math.Clamp(Math.Round((peakDecibels + 54.0) / 54.0 * (segmentCount - 1)), 0, segmentCount - 1);

        int meterStartX = startX + labelWidth;

        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            Color segmentColor = DetermineSegmentColor(segmentIndex, segmentCount, segmentIndex < activeSegments);
            float posX = meterStartX + segmentIndex * segmentStep;
            using var segmentBrush = new SolidBrush(segmentColor);
            using var segmentPath = Theme.Rounded(new RectangleF(posX, startY, segmentWidth, barHeight), 1.5f);
            canvas.FillPath(segmentBrush, segmentPath);

            // Draw Peak Hold tick
            if (segmentIndex == peakSegmentIndex && peakValue > 0.01f)
            {
                using var peakTickBrush = new SolidBrush(Theme.Ink);
                canvas.FillRectangle(peakTickBrush, posX + segmentWidth - 1.5f, startY, 1.5f, barHeight);
            }
        }
    }

    private static Color DetermineSegmentColor(int index, int total, bool isActive)
    {
        if (!isActive) return Theme.MeterTrack;
        if (index >= total - 3) return Theme.MeterPeak;
        if (index >= total - 7) return Theme.MeterWarning;
        return Theme.MeterNormal;
    }

    private void DrawClipIndicator(Graphics canvas, int startX, int startY, int boxWidth, int boxHeight)
    {
        var clipBounds = new RectangleF(startX, startY, boxWidth, boxHeight);
        using var clipPath = Theme.Rounded(clipBounds, 3f);
        using var fillBrush = new SolidBrush(isClipped ? Theme.Red : Theme.MeterTrack);
        canvas.FillPath(fillBrush, clipPath);

        using var font = Theme.MonoFont(7.5f, true);
        Color textColor = isClipped ? Color.White : Theme.Muted;
        TextRenderer.DrawText(canvas, "CLIP", font, Rectangle.Round(clipBounds), textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
