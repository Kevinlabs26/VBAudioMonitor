using System.ComponentModel;

namespace VBAudioMonitor;

/// <summary>
/// A flat modern button with stateful theme rendering and rounded corners.
/// </summary>
internal sealed class ActionButton : Button
{
    [DefaultValue(false)] public bool Primary { get; set; }
    [DefaultValue(false)] public bool Danger { get; set; }
    [DefaultValue(false)] public bool Compact { get; set; }

    private bool isHovered;
    private bool isPressed;

    public ActionButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        isHovered = true;
        Invalidate();
        base.OnMouseEnter(eventArgs);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        isHovered = false;
        isPressed = false;
        Invalidate();
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnMouseDown(MouseEventArgs mouseArgs)
    {
        if (mouseArgs.Button == MouseButtons.Left)
        {
            isPressed = true;
            Invalidate();
        }
        base.OnMouseDown(mouseArgs);
    }

    protected override void OnMouseUp(MouseEventArgs mouseArgs)
    {
        isPressed = false;
        Invalidate();
        base.OnMouseUp(mouseArgs);
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        paintEventArgs.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        paintEventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        Color fillColor;
        Color textColor;
        Color borderColor = Color.Transparent;

        if (!Enabled)
        {
            fillColor = Theme.Disabled;
            textColor = Theme.Muted;
        }
        else if (Danger)
        {
            fillColor = isPressed ? Color.FromArgb(190, 18, 60) : (isHovered ? Color.FromArgb(225, 29, 72) : Color.FromArgb(244, 63, 94));
            textColor = Color.White;
        }
        else if (Primary)
        {
            fillColor = isPressed ? Color.FromArgb(29, 78, 216) : (isHovered ? Theme.BlueHover : Theme.Blue);
            textColor = Color.White;
        }
        else
        {
            fillColor = isPressed ? Theme.Selected : (isHovered ? Theme.Hover : Theme.Input);
            textColor = Theme.Ink;
            borderColor = isHovered ? Theme.Border : Theme.BorderLight;
        }

        var buttonBounds = new RectangleF(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
        float cornerRadius = (Compact ? 6f : 8f) * DeviceDpi / 96f;
        using var roundedPath = Theme.Rounded(buttonBounds, cornerRadius);
        using var backgroundBrush = new SolidBrush(fillColor);
        paintEventArgs.Graphics.FillPath(backgroundBrush, roundedPath);

        if (borderColor != Color.Transparent && Enabled)
        {
            using var borderPen = new Pen(borderColor, Math.Max(1f, DeviceDpi / 96f));
            paintEventArgs.Graphics.DrawPath(borderPen, roundedPath);
        }

        TextRenderer.DrawText(paintEventArgs.Graphics, Text, Font, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(paintEventArgs.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), Theme.Ink, fillColor);
        }
    }
}
