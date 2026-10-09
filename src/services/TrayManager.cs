namespace VBAudioMonitor;

/// <summary>
/// Manages system tray integration, background persistence, and quick context menu actions.
/// </summary>
internal sealed class TrayManager : IDisposable
{
    private readonly Form hostForm;
    private readonly NotifyIcon systemTrayIcon;
    private readonly ToolStripMenuItem toggleActionItem;
    private readonly ToolStripMenuItem muteActionItem;
    private readonly ToolStripMenuItem profilesMenu;
    private readonly ToolStripMenuItem showWindowItem;
    private readonly ToolStripMenuItem exitItem;
    private readonly ToolStripMenuItem updatesItem;
    private bool updateReady;
    private readonly ContextMenuStrip trayMenu;

    public event Action? ToggleMonitoringRequested;
    public event Action? ToggleMuteRequested;
    public event Action? ExitRequested;
    public event Action? UpdatesRequested;
    public event Action<ListeningProfile>? ProfileSelected;

    public TrayManager(Form mainForm, Icon applicationIcon)
    {
        hostForm = mainForm;
        trayMenu = new ContextMenuStrip();

        showWindowItem = new ToolStripMenuItem(UiText.T("显示主界面"), null, (_, _) => RestoreMainWindow());
        showWindowItem.Font = new Font(showWindowItem.Font, FontStyle.Bold);

        toggleActionItem = new ToolStripMenuItem(UiText.T("开始监听"), null, (_, _) => ToggleMonitoringRequested?.Invoke());
        muteActionItem = new ToolStripMenuItem(UiText.T("静音"), null, (_, _) => ToggleMuteRequested?.Invoke());
        profilesMenu = new ToolStripMenuItem(UiText.T("切换场景方案"));
        exitItem = new ToolStripMenuItem(UiText.T("退出"), null, (_, _) => ExitRequested?.Invoke());
        updatesItem = new ToolStripMenuItem(UiText.T("检查更新"), null, (_, _) => UpdatesRequested?.Invoke());

        trayMenu.Items.AddRange([
            showWindowItem,
            new ToolStripSeparator(),
            toggleActionItem,
            muteActionItem,
            profilesMenu,
            updatesItem,
            new ToolStripSeparator(),
            exitItem
        ]);
        UpdateProfiles([]);

        systemTrayIcon = new NotifyIcon
        {
            Icon = applicationIcon,
            Text = UiText.T("VB 音频监听"),
            ContextMenuStrip = trayMenu,
            Visible = true
        };

        systemTrayIcon.DoubleClick += (_, _) => RestoreMainWindow();
    }

    /// <summary>
    /// Updates the state labels of tray menu items.
    /// </summary>
    public void UpdateMenuStates(bool isMonitoringRunning, bool isMuted, bool isRecovering = false)
    {
        toggleActionItem.Text = isRecovering ? UiText.T("停止等待") : isMonitoringRunning ? UiText.T("停止监听") : UiText.T("开始监听");
        muteActionItem.Text = isMuted ? UiText.T("取消静音") : UiText.T("快速静音");
        systemTrayIcon.Text = isRecovering ? UiText.T("VB 音频监听 (等待设备)") : isMonitoringRunning ? UiText.T("VB 音频监听 (运行中)") : UiText.T("VB 音频监听 (就绪)");
    }

    public void UpdateLanguage()
    {
        showWindowItem.Text = UiText.T("显示主界面");
        profilesMenu.Text = UiText.T("切换场景方案");
        exitItem.Text = UiText.T("退出");
        SetUpdateReady(updateReady);
    }

    public void SetUpdateReady(bool ready)
    {
        updateReady = ready;
        updatesItem.Text = UiText.T(ready ? "安装更新" : "检查更新");
    }

    public void UpdateProfiles(IEnumerable<ListeningProfile> profiles, ListeningProfile? selectedProfile = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (profilesMenu.DropDown.Visible) profilesMenu.DropDown.Close();
        while (profilesMenu.DropDownItems.Count > 0)
        {
            var oldItem = profilesMenu.DropDownItems[0];
            profilesMenu.DropDownItems.RemoveAt(0);
            oldItem.Dispose();
        }
        foreach (var profile in profiles)
        {
            var item = new ToolStripMenuItem(profile.Name.Replace("&", "&&"))
            {
                Checked = profile == selectedProfile
            };
            item.Click += (_, _) => ProfileSelected?.Invoke(profile);
            profilesMenu.DropDownItems.Add(item);
        }
        if (profilesMenu.DropDownItems.Count == 0)
            profilesMenu.DropDownItems.Add(new ToolStripMenuItem(UiText.T("尚未保存方案")) { Enabled = false });
    }

    /// <summary>
    /// Restores the main form from tray and brings it to front.
    /// </summary>
    public void RestoreMainWindow()
    {
        if (!hostForm.Visible)
        {
            hostForm.Show();
        }
        if (hostForm.WindowState == FormWindowState.Minimized)
        {
            hostForm.WindowState = FormWindowState.Normal;
        }
        hostForm.Activate();
        hostForm.BringToFront();
    }

    /// <summary>
    /// Minimizes the host form to the system tray.
    /// </summary>
    public void MinimizeToTray()
    {
        hostForm.Hide();
    }

    public void Dispose()
    {
        systemTrayIcon.Visible = false;
        systemTrayIcon.Dispose();
        trayMenu.Dispose();
    }
}
