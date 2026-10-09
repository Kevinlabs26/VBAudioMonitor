namespace VBAudioMonitor;

internal sealed class DiagnosticsForm : Form
{
    public DiagnosticsForm(DiagnosticReport report)
    {
        SuspendLayout();
        Text = "音频检查";
        Font = Theme.TextFont(14f);
        ForeColor = Theme.Ink;
        BackColor = Theme.Background;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 460);
        MinimumSize = new Size(520, 360);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var root = MonitorFormLayout.CreateStack(64, 12, -100, 12, 40);
        root.Padding = new Padding(20);
        var header = MonitorFormLayout.CreateStack(34, 30);
        var title = MonitorFormLayout.CreateLabel("音频检查", 18f, true);
        MonitorFormLayout.PlaceCell(header, title, 0);
        var headline = MonitorFormLayout.CreateLabel(report.Headline, 13f, true);
        headline.AccessibleName = "诊断结果";
        MonitorFormLayout.PlaceCell(header, headline, 1);
        MonitorFormLayout.PlaceCell(root, header, 0);

        var surface = new Surface { Padding = new Padding(12) };
        var details = new RichTextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Regular, GraphicsUnit.Point),
            BackColor = Theme.Surface,
            ForeColor = Theme.Ink,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true,
            DetectUrls = false,
            Text = report.Details + Environment.NewLine,
            AccessibleName = "音频检查详情"
        };
        surface.Controls.Add(details);
        MonitorFormLayout.PlaceCell(root, surface, 2);
        var footer = MonitorFormLayout.CreateColumns(-100, 104);
        var close = new ActionButton { Text = "关闭", Primary = true };
        close.Click += (_, _) => Close();
        MonitorFormLayout.PlaceCell(footer, close, 0, 1);
        MonitorFormLayout.PlaceCell(root, footer, 4);
        Controls.Add(root);
        AcceptButton = CancelButton = close;
        Theme.ApplyTheme(this);
        UiText.Apply(this);
        headline.ForeColor = report.NeedsAttention ? Theme.Amber : Theme.Green;
        ResumeLayout(true);
    }
}
