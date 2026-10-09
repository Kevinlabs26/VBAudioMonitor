namespace VBAudioMonitor;

internal sealed class ProfileNameForm : Form
{
    private readonly TextBox nameInput = new()
    {
        MaxLength = 24,
        AutoSize = false,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        AccessibleName = "方案名称"
    };

    public string ProfileName => nameInput.Text.Trim();

    public ProfileNameForm(string initialName = "")
    {
        SuspendLayout();
        Text = "保存场景方案";
        Font = Theme.TextFont(14f);
        ForeColor = Theme.Ink;
        BackColor = Theme.Background;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 240);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var root = MonitorFormLayout.CreateStack(34, 48, 8, 42, -100, 40);
        root.Padding = new Padding(24);
        var title = MonitorFormLayout.CreateLabel("保存场景方案", 18f, true);
        MonitorFormLayout.PlaceCell(root, title, 0);
        var explanation = MonitorFormLayout.CreateLabel("保存当前线路、声道、缓冲与监听音量。\n名称限 1–24 个字符，同名方案会更新。", 11.5f);
        MonitorFormLayout.PlaceCell(root, explanation, 1);
        var inputSurface = new Surface { Padding = new Padding(12, 9, 12, 9) };
        nameInput.Font = new Font(Font.FontFamily, 10f, FontStyle.Regular, GraphicsUnit.Point);
        inputSurface.Controls.Add(nameInput);
        MonitorFormLayout.PlaceCell(root, inputSurface, 3);

        var actions = MonitorFormLayout.CreateColumns(-100, 104, 12, 128);
        var cancel = new ActionButton { Text = "取消", DialogResult = DialogResult.Cancel };
        var save = new ActionButton { Text = "保存方案", Primary = true, DialogResult = DialogResult.OK };
        MonitorFormLayout.PlaceCell(actions, cancel, 0, 1);
        MonitorFormLayout.PlaceCell(actions, save, 0, 3);
        MonitorFormLayout.PlaceCell(root, actions, 5);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        nameInput.TextChanged += (_, _) => save.Enabled = ListeningProfile.IsValidName(nameInput.Text);
        nameInput.Text = initialName;
        save.Enabled = ListeningProfile.IsValidName(nameInput.Text);
        Shown += (_, _) => { nameInput.Focus(); nameInput.SelectAll(); };
        Theme.ApplyTheme(this);
        UiText.Apply(this);
        inputSurface.BackColor = nameInput.BackColor = Theme.Input;
        nameInput.ForeColor = Theme.Ink;
        ResumeLayout(true);
    }
}
