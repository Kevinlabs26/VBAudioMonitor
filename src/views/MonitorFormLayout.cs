namespace VBAudioMonitor;

/// <summary>
/// Shared table layout and typography helpers.
/// </summary>
internal static class MonitorFormLayout
{
    public static TableLayoutPanel CreateStack(params float[] rowHeights)
    {
        var stackPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rowHeights.Length,
            Margin = Padding.Empty
        };
        stackPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (float heightValue in rowHeights)
        {
            stackPanel.RowStyles.Add(new RowStyle(heightValue < 0 ? SizeType.Percent : SizeType.Absolute, Math.Abs(heightValue)));
        }
        return stackPanel;
    }

    public static TableLayoutPanel CreateColumns(params float[] columnWidths)
    {
        var columnsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columnWidths.Length,
            RowCount = 1,
            Margin = Padding.Empty
        };
        foreach (float widthValue in columnWidths)
        {
            columnsPanel.ColumnStyles.Add(new ColumnStyle(widthValue < 0 ? SizeType.Percent : SizeType.Absolute, Math.Abs(widthValue)));
        }
        columnsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return columnsPanel;
    }

    public static void PlaceCell(TableLayoutPanel targetPanel, Control childControl, int targetRow = 0, int targetColumn = 0)
    {
        childControl.Dock = DockStyle.Fill;
        childControl.Margin = Padding.Empty;
        targetPanel.Controls.Add(childControl, targetColumn, targetRow);
    }

    public static Label CreateLabel(string labelText, float fontSize = 13f, bool isBold = false, ContentAlignment alignment = ContentAlignment.MiddleLeft) => new()
    {
        Text = UiText.T(labelText),
        Tag = labelText,
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = alignment,
        Font = Theme.TextFont(fontSize, isBold),
        ForeColor = isBold ? Theme.Ink : Theme.Muted,
        Margin = Padding.Empty,
        AutoEllipsis = true
    };

    public static Panel WrapSelector(ComboBox dropDownControl)
    {
        var selectorPanel = new Surface
        {
            BackColor = Theme.Input,
            CornerRadius = 7f,
            DrawBorder = true,
            Padding = new Padding(6, 4, 6, 4)
        };
        dropDownControl.Dock = DockStyle.Fill;
        selectorPanel.Controls.Add(dropDownControl);
        return selectorPanel;
    }
}
