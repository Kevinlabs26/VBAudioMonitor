namespace VBAudioMonitor;

/// <summary>
/// A styled flat drop-down selector for audio devices and stream options.
/// </summary>
internal sealed class DeviceSelector : ComboBox
{
    public DeviceSelector()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Input;
        ForeColor = Theme.Ink;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 36;
        IntegralHeight = false;
        DropDownHeight = 320;
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Input;
        ForeColor = Theme.Ink;
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs drawArgs)
    {
        bool isSelected = (drawArgs.State & DrawItemState.Selected) != 0 && (drawArgs.State & DrawItemState.ComboBoxEdit) == 0;
        using var backgroundBrush = new SolidBrush(isSelected ? Theme.Selected : BackColor);
        drawArgs.Graphics.FillRectangle(backgroundBrush, drawArgs.Bounds);

        string itemDisplayName = drawArgs.Index >= 0 ? Items[drawArgs.Index]?.ToString() ?? "" : UiText.T("请选择设备");
        var textBounds = new Rectangle(drawArgs.Bounds.X + 8, drawArgs.Bounds.Y, Math.Max(0, drawArgs.Bounds.Width - 16), drawArgs.Bounds.Height);

        TextRenderer.DrawText(drawArgs.Graphics, itemDisplayName, Font, textBounds, Enabled ? Theme.Ink : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        base.OnDrawItem(drawArgs);
    }
}
