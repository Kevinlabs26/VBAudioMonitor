namespace VBAudioMonitor;

internal sealed class UpdateForm : Form
{
    private readonly AppUpdates updates;
    private readonly Label status = MonitorFormLayout.CreateLabel("", 12f);
    private readonly ActionButton check = new() { Text = "检查更新", Compact = true };
    private readonly ActionButton install = new() { Text = "立即安装", Primary = true, Compact = true, DialogResult = DialogResult.OK };

    public UpdateForm(AppUpdates updates)
    {
        this.updates = updates;
        SuspendLayout();
        Text = "软件更新";
        Font = Theme.TextFont(14f);
        ForeColor = Theme.Ink;
        BackColor = Theme.Background;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        var root = MonitorFormLayout.CreateStack(36, 28, 12, 74, 72, -100, 40);
        root.Padding = new Padding(24);
        MonitorFormLayout.PlaceCell(root, MonitorFormLayout.CreateLabel("软件更新", 18f, true), 0);
        var version = MonitorFormLayout.CreateLabel(UiText.T("当前版本：{0}", updates.Version), 12f);
        MonitorFormLayout.PlaceCell(root, version, 1);
        status.TextAlign = ContentAlignment.TopLeft;
        MonitorFormLayout.PlaceCell(root, status, 3);
        var explanation = MonitorFormLayout.CreateLabel("后台下载，下次启动自动安装。\n立即安装会保存设置、停止监听并重启程序。", 11.5f);
        explanation.TextAlign = ContentAlignment.TopLeft;
        MonitorFormLayout.PlaceCell(root, explanation, 4);
        var actions = MonitorFormLayout.CreateColumns(-100, 152, 12, 152, 12, 88);
        var close = new ActionButton { Text = "关闭", Compact = true, DialogResult = DialogResult.Cancel };
        MonitorFormLayout.PlaceCell(actions, check, 0, 1);
        MonitorFormLayout.PlaceCell(actions, install, 0, 3);
        MonitorFormLayout.PlaceCell(actions, close, 0, 5);
        MonitorFormLayout.PlaceCell(root, actions, 6);
        Controls.Add(root);
        CancelButton = close;
        check.Click += async (_, _) => await updates.CheckAndDownloadAsync();
        updates.StateChanged += RefreshState;
        Theme.ApplyTheme(this);
        UiText.Apply(this);
        RefreshState();
        ResumeLayout(true);
    }

    private void RefreshState()
    {
        if (IsDisposed) return;
        status.Text = updates.StatusText;
        check.Enabled = updates.CanUpdate && !updates.IsBusy && !updates.ReadyToRestart;
        install.Enabled = updates.ReadyToRestart && !updates.IsBusy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) updates.StateChanged -= RefreshState;
        base.Dispose(disposing);
    }
}
