using System.Drawing.Drawing2D;

namespace VBAudioMonitor;

/// <summary>
/// Provides unified color palette, typography, and geometry drawing utilities.
/// </summary>
internal static class Theme
{
    public static bool IsDark { get; private set; }

    public static Color Background => IsDark ? Color.FromArgb(15, 20, 28) : Color.FromArgb(243, 246, 251);
    public static Color Surface => IsDark ? Color.FromArgb(24, 32, 45) : Color.White;
    public static Color SurfaceAlt => IsDark ? Color.FromArgb(30, 40, 56) : Color.FromArgb(248, 250, 253);
    public static Color Input => IsDark ? Color.FromArgb(33, 44, 62) : Color.FromArgb(244, 247, 252);
    public static Color Ink => IsDark ? Color.FromArgb(240, 245, 253) : Color.FromArgb(20, 32, 54);
    public static Color Muted => IsDark ? Color.FromArgb(145, 162, 185) : Color.FromArgb(108, 122, 145);
    public static Color Border => IsDark ? Color.FromArgb(46, 60, 82) : Color.FromArgb(224, 230, 240);
    public static Color BorderLight => IsDark ? Color.FromArgb(38, 50, 70) : Color.FromArgb(235, 240, 248);
    public static Color Track => IsDark ? Color.FromArgb(42, 55, 75) : Color.FromArgb(230, 236, 245);

    // Accent and acoustic colors
    public static Color Blue => IsDark ? Color.FromArgb(92, 144, 255) : Color.FromArgb(37, 99, 235);
    public static Color BlueHover => IsDark ? Color.FromArgb(115, 162, 255) : Color.FromArgb(29, 78, 216);
    public static Color BlueLight => IsDark ? Color.FromArgb(30, 50, 85) : Color.FromArgb(232, 241, 255);
    public static Color Green => IsDark ? Color.FromArgb(64, 210, 155) : Color.FromArgb(16, 160, 116);
    public static Color Amber => IsDark ? Color.FromArgb(250, 188, 70) : Color.FromArgb(217, 119, 6);
    public static Color Red => IsDark ? Color.FromArgb(255, 107, 125) : Color.FromArgb(225, 29, 72);
    public static Color Disabled => IsDark ? Color.FromArgb(35, 45, 62) : Color.FromArgb(238, 242, 248);
    public static Color Hover => IsDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(232, 238, 248);
    public static Color Selected => IsDark ? Color.FromArgb(42, 66, 105) : Color.FromArgb(226, 237, 255);

    // Acoustic meter specific palettes
    public static Color MeterNormal => IsDark ? Color.FromArgb(46, 204, 113) : Color.FromArgb(16, 160, 116);
    public static Color MeterWarning => IsDark ? Color.FromArgb(241, 196, 15) : Color.FromArgb(220, 150, 10);
    public static Color MeterPeak => IsDark ? Color.FromArgb(231, 76, 60) : Color.FromArgb(225, 29, 72);
    public static Color MeterTrack => IsDark ? Color.FromArgb(32, 42, 58) : Color.FromArgb(228, 233, 242);

    public static void SetDark(bool enableDarkMode) => IsDark = enableDarkMode;

    public static Color Badge(Color accentForeground)
    {
        Color baseColor = Surface;
        float blendFactor = IsDark ? 0.22f : 0.12f;
        return Color.FromArgb(
            Math.Clamp(baseColor.R + (int)((accentForeground.R - baseColor.R) * blendFactor), 0, 255),
            Math.Clamp(baseColor.G + (int)((accentForeground.G - baseColor.G) * blendFactor), 0, 255),
            Math.Clamp(baseColor.B + (int)((accentForeground.B - baseColor.B) * blendFactor), 0, 255));
    }

    public static Font TextFont(float pointSize, bool isBold = false) => new("Microsoft YaHei UI",
        Math.Max(pointSize, 11.5f) * 96f / 72f, isBold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);

    public static Font MonoFont(float pointSize, bool isBold = false) => new("Consolas",
        Math.Max(pointSize, 9.5f) * 96f / 72f, isBold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);

    public static GraphicsPath Rounded(RectangleF boundaryRect, float cornerRadius)
    {
        float arcDiameter = Math.Max(1f, Math.Min(cornerRadius * 2, Math.Min(boundaryRect.Width, boundaryRect.Height)));
        var roundedPath = new GraphicsPath();
        roundedPath.AddArc(boundaryRect.X, boundaryRect.Y, arcDiameter, arcDiameter, 180, 90);
        roundedPath.AddArc(boundaryRect.Right - arcDiameter, boundaryRect.Y, arcDiameter, arcDiameter, 270, 90);
        roundedPath.AddArc(boundaryRect.Right - arcDiameter, boundaryRect.Bottom - arcDiameter, arcDiameter, arcDiameter, 0, 90);
        roundedPath.AddArc(boundaryRect.X, boundaryRect.Bottom - arcDiameter, arcDiameter, arcDiameter, 90, 90);
        roundedPath.CloseFigure();
        return roundedPath;
    }

    public static void DrawMark(Graphics targetGraphics, RectangleF targetBounds)
    {
        targetGraphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var tilePath = Rounded(targetBounds, targetBounds.Width * 0.28f);
        using var gradientFill = new LinearGradientBrush(targetBounds,
            IsDark ? Color.FromArgb(70, 130, 255) : Color.FromArgb(37, 99, 235),
            IsDark ? Color.FromArgb(40, 90, 220) : Color.FromArgb(25, 70, 200),
            LinearGradientMode.ForwardDiagonal);
        targetGraphics.FillPath(gradientFill, tilePath);

        using var strokePen = new Pen(Color.White, Math.Max(2f, targetBounds.Width * 0.08f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        float[] waveHeights = [0.22f, 0.48f, 0.72f, 0.42f, 0.22f];
        for (int barIndex = 0; barIndex < waveHeights.Length; barIndex++)
        {
            float posX = targetBounds.X + targetBounds.Width * (0.24f + barIndex * 0.13f);
            float posY = targetBounds.Y + targetBounds.Height * (1 - waveHeights[barIndex]) / 2f;
            targetGraphics.DrawLine(strokePen, posX, posY, posX, posY + targetBounds.Height * waveHeights[barIndex]);
        }
    }

    public static Icon CreateIcon()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream("VBAudioMonitor.assets.app.ico")
            ?? throw new InvalidOperationException("The embedded application icon is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    public static void ApplyTheme(Control targetControl, bool isInsideCard = false)
    {
        bool isCardScope = isInsideCard || targetControl is Surface;
        if (targetControl is Form formInstance) formInstance.BackColor = Background;
        else if (targetControl is Surface surfaceInstance)
        {
            surfaceInstance.BackColor = surfaceInstance.Controls.Count > 0 && surfaceInstance.Controls[0] is DeviceSelector ? Input : Surface;
            surfaceInstance.Invalidate();
        }
        else if (targetControl is TableLayoutPanel tablePanel) tablePanel.BackColor = isCardScope ? Surface : Background;
        else if (targetControl is DeviceSelector selector) selector.ApplyTheme();
        else if (targetControl is SmoothSlider slider) slider.ApplyTheme();
        else if (targetControl is LevelMeter meter) meter.ApplyTheme();
        else if (targetControl is BrandMark brand) { brand.BackColor = Background; brand.Invalidate(); }
        else if (targetControl is ActionButton button) button.Invalidate();
        else if (targetControl is BadgeBox badge) badge.Invalidate();
        else if (targetControl is Label label) label.ForeColor = label.Font.Bold ? Ink : Muted;

        foreach (Control childControl in targetControl.Controls)
        {
            ApplyTheme(childControl, isCardScope);
        }
    }
}
