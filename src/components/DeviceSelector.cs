using System.Drawing.Drawing2D;

namespace VBAudioMonitor;

/// <summary>
/// A styled flat drop-down selector for audio devices and stream options.
/// </summary>
internal sealed class DeviceSelector : ComboBox
{
    private bool isHovered;

    public DeviceSelector()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        FormattingEnabled = true;
        BackColor = Theme.Input;
        ForeColor = Theme.Ink;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 36;
        IntegralHeight = false;
        DropDownHeight = 320;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // The field paints its own border; keep the native list and keyboard behavior.
            parameters.ExStyle &= ~0x200; // WS_EX_CLIENTEDGE
            parameters.Style &= ~0x800000; // WS_BORDER
            return parameters;
        }
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Input;
        ForeColor = Theme.Ink;
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs drawArgs)
    {
        if ((drawArgs.State & DrawItemState.ComboBoxEdit) != 0)
        {
            Invalidate();
            return;
        }
        bool isSelected = (drawArgs.State & DrawItemState.Selected) != 0;
        using var backgroundBrush = new SolidBrush(isSelected ? Theme.Selected : BackColor);
        drawArgs.Graphics.FillRectangle(backgroundBrush, drawArgs.Bounds);

        string itemDisplayName = drawArgs.Index >= 0 ? GetItemText(Items[drawArgs.Index]) ?? "" : UiText.T("请选择设备");
        int inset = (int)Math.Round(12 * DeviceDpi / 96f);
        var textBounds = new Rectangle(drawArgs.Bounds.X + inset, drawArgs.Bounds.Y,
            Math.Max(0, drawArgs.Bounds.Width - inset * 2), drawArgs.Bounds.Height);

        TextRenderer.DrawText(drawArgs.Graphics, itemDisplayName, Font, textBounds, Enabled ? Theme.Ink : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        base.OnDrawItem(drawArgs);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Control? backdrop = Parent;
        while (backdrop?.BackColor.A == 0) backdrop = backdrop.Parent;
        e.Graphics.Clear(backdrop?.BackColor ?? Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        bool active = Enabled && (Focused || DroppedDown);
        Color fill = !Enabled ? Theme.Disabled : isHovered || DroppedDown ? Theme.Hover : Theme.Input;
        using var path = Theme.Rounded(new RectangleF(scale, scale,
            Math.Max(1, Width - scale * 2), Math.Max(1, Height - scale * 2)), 7 * scale);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(active ? Theme.Blue : isHovered && Enabled ? Theme.Muted : Theme.Border,
            (active ? 1.5f : 1f) * scale);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);

        int inset = (int)Math.Round(14 * scale);
        int arrowArea = (int)Math.Round(36 * scale);
        bool rtl = RightToLeft == RightToLeft.Yes;
        var textBounds = new Rectangle(rtl ? arrowArea : inset, 0,
            Math.Max(0, Width - inset - arrowArea), Height);
        string label = SelectedIndex >= 0 ? GetItemText(SelectedItem) ?? "" : UiText.T("请选择设备");
        TextRenderer.DrawText(e.Graphics, label, Font, textBounds, Enabled ? Theme.Ink : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
            | (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));

        float arrowX = rtl ? 18 * scale : Width - 18 * scale;
        float arrowY = Height / 2f;
        float direction = DroppedDown ? -1 : 1;
        using var arrow = new Pen(active ? Theme.Blue : Theme.Muted, 1.8f * scale)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        e.Graphics.DrawLines(arrow, [new PointF(arrowX - 4 * scale, arrowY - 2 * scale * direction),
            new PointF(arrowX, arrowY + 2 * scale * direction),
            new PointF(arrowX + 4 * scale, arrowY - 2 * scale * direction)]);
    }

    protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
    protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
}
