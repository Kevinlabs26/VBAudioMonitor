namespace VBAudioMonitor;

/// <summary>
/// A non-interactive status pill indicator distinct from clickable buttons.
/// </summary>
internal sealed class BadgeBox : Control
{
    private string displayText = "";
    private Color accentTone = Theme.Blue;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => displayText;
        set { displayText = value ?? ""; Invalidate(); }
    }

    public Color AccentColor
    {
        get => accentTone;
        set { accentTone = value; Invalidate(); }
    }

    public BadgeBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void ApplyTheme() => Invalidate();

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        paintEventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var capsuleBounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var capsulePath = Theme.Rounded(capsuleBounds, capsuleBounds.Height / 2f);
        using var fillBrush = new SolidBrush(Theme.Badge(accentTone));
        paintEventArgs.Graphics.FillPath(fillBrush, capsulePath);

        using var borderPen = new Pen(Color.FromArgb(40, accentTone), 1f);
        paintEventArgs.Graphics.DrawPath(borderPen, capsulePath);

        TextRenderer.DrawText(paintEventArgs.Graphics, displayText, Font, ClientRectangle, accentTone,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}
