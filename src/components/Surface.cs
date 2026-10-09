using System.Drawing.Drawing2D;

namespace VBAudioMonitor;

/// <summary>
/// A sleek rounded container surface with adaptive theme border.
/// </summary>
internal class Surface : Panel
{
    public float CornerRadius { get; set; } = 10f;
    public bool DrawBorder { get; set; } = true;

    public Surface()
    {
        BackColor = Theme.Surface;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        paintEventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var surfaceBounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var roundedPath = Theme.Rounded(surfaceBounds, CornerRadius * DeviceDpi / 96f);
        using var backgroundBrush = new SolidBrush(BackColor);
        paintEventArgs.Graphics.FillPath(backgroundBrush, roundedPath);

        if (DrawBorder)
        {
            using var borderPen = new Pen(Theme.Border, Math.Max(1f, DeviceDpi / 96f));
            paintEventArgs.Graphics.DrawPath(borderPen, roundedPath);
        }
    }
}
