using NAudio.CoreAudioApi.Interfaces;

namespace VBAudioMonitor;

/// <summary>
/// Main studio control form orchestrating audio streams, telemetry, and system interactions.
/// </summary>
internal sealed class MonitorForm : Form
{
    private readonly DeviceSelector sourceSelector = new() { AccessibleName = "声音来源" };
    private readonly DeviceSelector targetSelector = new() { AccessibleName = "监听目标" };
    private readonly DeviceSelector channelSelector = new() { AccessibleName = "监听声道" };
    private readonly DeviceSelector latencySelector = new() { AccessibleName = "音频缓冲" };
    private readonly DeviceSelector profileSelector = new() { AccessibleName = "场景方案" };
    private readonly DeviceSelector languageSelector = new() { AccessibleName = "界面语言", ItemHeight = 26 };
    private readonly ActionButton saveProfileButton = new() { Text = "保存方案", Compact = true, AccessibleName = "保存方案" };
    private readonly ActionButton deleteProfileButton = new() { Text = "删除", Compact = true, AccessibleName = "删除方案" };
    private readonly SmoothSlider gainSlider = new();
    private readonly Label gainLabel = new() { Text = "75.0%", AutoSize = false, Dock = DockStyle.Fill, Font = Theme.MonoFont(30f, true), ForeColor = Theme.Blue };
    private readonly ActionButton muteButton = new() { Text = "静音", Compact = true, AccessibleName = "静音切换" };
    private readonly ActionButton diagnosticsButton = new() { Text = "检查音频", Compact = true, AccessibleName = "检查音频" };

    private readonly Label sourceInfo = MonitorFormLayout.CreateLabel("与网页选项一致", 11.5f);
    private readonly Label targetInfo = MonitorFormLayout.CreateLabel("真实物理输出设备", 11.5f);
    private readonly Label statusMessage = MonitorFormLayout.CreateLabel("确认设备后点击开启监听", 12f);
    private readonly Label statusTitle = MonitorFormLayout.CreateLabel("准备就绪", 15f, true);
    private readonly BadgeBox engineBadge = new() { Text = "● 就绪", AccentColor = Theme.Blue };

    private readonly Label decibelLabel = new() { Text = "-inf dBFS", AutoSize = false, Dock = DockStyle.Fill, Font = Theme.MonoFont(12f, true), ForeColor = Theme.Muted };
    private readonly Label latencyStatLabel = new() { Text = "0 ms", AutoSize = false, Dock = DockStyle.Fill, Font = Theme.MonoFont(13f, true), ForeColor = Theme.Blue };
    private readonly Label underrunLabel = new() { Text = "0 次", AutoSize = false, Dock = DockStyle.Fill, Font = Theme.MonoFont(13f, true), ForeColor = Theme.Green };

    private readonly LevelMeter stereoMeter = new();
    private readonly ActionButton masterToggleButton = new() { Text = "开始监听", Primary = true, AccessibleName = "开始或停止监听" };
    private readonly ActionButton refreshButton = new() { Text = "刷新设备" };
    private readonly ActionButton themeToggleButton = new();
    private readonly ActionButton minimizeTrayButton = new() { Text = "隐藏到托盘" };
    private readonly ActionButton exitButton = new() { Text = "退出程序", AccessibleName = "退出程序" };
    private readonly ActionButton updatesButton = new() { Text = "软件更新", Compact = true, AccessibleName = "软件更新" };
    private readonly AppUpdates appUpdates;
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 21600000 };

    private readonly System.Windows.Forms.Timer telemetryTimer = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer preferencesTimer = new() { Interval = 500 };
    private readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 2000 };
    private readonly ToolTip tooltipHelper = new() { AutoPopDelay = 12000 };
    private readonly Icon applicationIcon = Theme.CreateIcon();
    private readonly TrayManager trayManager;

    private AudioRelay? activeRelay;
    private Preferences? currentPreferences;
    private int preMuteGain = 750;
    private bool isSystemExiting;
    private bool isLoadingDevices = true;
    private bool preferencesSaveFailed;
    private string statusExplanation = "";
    private object?[] statusArguments = [];
    private string statusBadge = "就绪";
    private readonly bool persistPreferences;
    private DateTime lastSignalTime;
    private bool hasReceivedSignal;
    private readonly Dictionary<string, int> deviceVolumes;
    private readonly Dictionary<string, int> deviceUnmuteVolumes;
    private string? gainTargetId;
    private string? lastAudioError;
    private ListeningRoute? connectedRoute;
    private ListeningRoute? recoveryRoute;
    private readonly List<ListeningProfile> profiles;
    private bool applyingProfile;
    private bool refreshingProfiles;
    private string? profileNameHint;

    private sealed record ListeningRoute(Endpoint Source, Endpoint Target, int Channel, int Latency);

    public MonitorForm(bool? overrideDark = null, AppUpdates? updates = null)
    {
        appUpdates = updates ?? new AppUpdates();
        SuspendLayout();
        Text = "VB 音频监听";
        Icon = applicationIcon;
        currentPreferences = PreferencesStorage.LoadPreferences() ?? new Preferences("", "", 0, 100, 750, Version: 2);
        deviceVolumes = new(currentPreferences.DeviceVolumes ?? new(), StringComparer.Ordinal);
        deviceUnmuteVolumes = new(currentPreferences.DeviceUnmuteVolumes ?? new(), StringComparer.Ordinal);
        currentPreferences = currentPreferences with { DeviceVolumes = deviceVolumes, DeviceUnmuteVolumes = deviceUnmuteVolumes };
        profiles = (currentPreferences.Profiles ?? []).Where(profile => profile is { IsValid: true })
            .Select(profile => profile with { Name = profile.Name.Trim() })
            .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.Last()).ToList();
        // Theme override is used by previews, which must not overwrite user settings.
        persistPreferences = !overrideDark.HasValue;

        if (overrideDark.HasValue) Theme.SetDark(overrideDark.Value);
        else if (currentPreferences != null) Theme.SetDark(currentPreferences.Dark);

        Font = Theme.TextFont(14f);
        ForeColor = Theme.Ink;
        BackColor = Theme.Background;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(720, 600);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        gainSlider.Value = currentPreferences?.SliderVolume ?? 750;
        themeToggleButton.Text = Theme.IsDark ? "浅色模式" : "深色模式";
        languageSelector.Items.AddRange(UiText.Languages.Cast<object>().ToArray());
        languageSelector.SelectedItem = UiText.Languages.Single(language => language.Code == UiText.Language);

        trayManager = new TrayManager(this, applicationIcon);
        trayManager.ToggleMonitoringRequested += ToggleMonitoring;
        trayManager.ToggleMuteRequested += ToggleMute;
        trayManager.ExitRequested += ExitApplication;
        trayManager.ProfileSelected += profile => { ApplyProfile(profile); trayManager.RestoreMainWindow(); };
        trayManager.UpdatesRequested += () => { trayManager.RestoreMainWindow(); ShowUpdates(); };
        appUpdates.StateChanged += UpdateUpdateFeedback;

        AssembleUserInterface();
        UiText.Apply(this);
        ApplyCurrentTheme();
        AttachEventHandlers();

        latencySelector.Items.AddRange([UiText.T("60 毫秒 · 低延迟"), UiText.T("100 毫秒 · 平衡"), UiText.T("200 毫秒 · 稳定")]);
        latencySelector.SelectedIndex = currentPreferences != null ? (currentPreferences.Latency switch { 60 => 0, 200 => 2, _ => 1 }) : 1;

        UpdateGainState();
        LoadDevices();
        RefreshProfiles();
        UpdateUpdateFeedback();
        ResumeLayout(true);
    }

    private void AssembleUserInterface()
    {
        var viewBuilder = new SignalChainView();
        var headerView = viewBuilder.BuildHeader(new BrandMark(), themeToggleButton, refreshButton, minimizeTrayButton, exitButton, engineBadge);
        var routeCard = viewBuilder.BuildRouteCard(sourceSelector, targetSelector, channelSelector, latencySelector, sourceInfo, targetInfo, stereoMeter, decibelLabel,
            profileSelector, saveProfileButton, deleteProfileButton);
        var controlCard = viewBuilder.BuildControlCard(gainSlider, gainLabel, muteButton, diagnosticsButton, masterToggleButton, statusTitle, statusMessage, latencyStatLabel, underrunLabel);
        var footerView = viewBuilder.BuildFooter(languageSelector, updatesButton);

        Controls.Add(viewBuilder.BuildRootView(headerView, routeCard, controlCard, footerView));
        sourceSelector.DropDownWidth = targetSelector.DropDownWidth = 560;
        profileSelector.DropDownWidth = 560;
        UpdateTooltips();
    }

    private void UpdateTooltips()
    {
        tooltipHelper.SetToolTip(profileSelector, UiText.T("选择方案会先停止监听，应用后点击开始监听。"));
        tooltipHelper.SetToolTip(saveProfileButton, UiText.T("保存当前设备、声道、缓冲和音量；同名方案会更新。"));
        tooltipHelper.SetToolTip(gainSlider, UiText.T("精准监听增益：仅作用于物理耳机，不影响虚拟线路原声。"));
        tooltipHelper.SetToolTip(latencySelector, UiText.T("若遇到偶发爆音或卡顿，请尝试切换至 200 毫秒缓冲。"));
        tooltipHelper.SetToolTip(languageSelector, UiText.T("选择界面语言，立即生效；不会停止监听。"));
    }

    private void AttachEventHandlers()
    {
        sourceSelector.SelectedIndexChanged += (_, _) => { RefreshChannels(); UpdateDeviceDetails(); RefreshIdleState(); MarkProfileChanged(); QueuePreferencesSave(); };
        targetSelector.SelectedIndexChanged += (_, _) => { if (!isLoadingDevices) RestoreTargetVolume(); UpdateDeviceDetails(); RefreshIdleState(); MarkProfileChanged(); QueuePreferencesSave(); };
        channelSelector.SelectedIndexChanged += (_, _) => { RefreshIdleState(); MarkProfileChanged(); QueuePreferencesSave(); };
        latencySelector.SelectedIndexChanged += (_, _) => { MarkProfileChanged(); QueuePreferencesSave(); };
        gainSlider.ValueChanged += (_, _) => { UpdateGainState(); RememberTargetVolume(); activeRelay?.SetVolume(gainSlider.Value / 1000f); MarkProfileChanged(); QueuePreferencesSave(); if (activeRelay == null) RefreshIdleState(); };
        profileSelector.SelectedIndexChanged += (_, _) =>
        {
            if (refreshingProfiles) return;
            if (profileSelector.SelectedItem is ListeningProfile profile) ApplyProfile(profile);
            else RefreshProfiles();
        };
        saveProfileButton.Click += (_, _) => { using var dialog = new ProfileNameForm(profileNameHint ?? "") { Icon = Icon }; if (dialog.ShowDialog(this) == DialogResult.OK) SaveProfile(dialog.ProfileName); };
        deleteProfileButton.Click += (_, _) =>
        {
            if (profileSelector.SelectedItem is ListeningProfile profile
                && MessageBox.Show(this, UiText.T("删除场景方案“{0}”？", profile.Name), UiText.T("删除场景方案"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) DeleteProfile(profile.Name);
        };
        masterToggleButton.Click += (_, _) => ToggleMonitoring();
        muteButton.Click += (_, _) => ToggleMute();
        diagnosticsButton.Click += (_, _) => ShowDiagnostics();
        refreshButton.Click += (_, _) => LoadDevices();
        themeToggleButton.Click += (_, _) => ToggleTheme();
        minimizeTrayButton.Click += (_, _) => trayManager.MinimizeToTray();
        exitButton.Click += (_, _) => ExitApplication();
        updatesButton.Click += (_, _) => ShowUpdates();
        Shown += async (_, _) =>
        {
            if (!persistPreferences || !appUpdates.CanUpdate) return;
            updateTimer.Start();
            await appUpdates.CheckAndDownloadAsync();
        };
        updateTimer.Tick += async (_, _) => await appUpdates.CheckAndDownloadAsync();
        languageSelector.SelectedIndexChanged += (_, _) =>
        {
            if (!isLoadingDevices && languageSelector.SelectedItem is UiLanguage language && language.Code != UiText.Language)
                ChangeLanguage(language.Code);
        };
        telemetryTimer.Tick += (_, _) => UpdateTelemetry();
        preferencesTimer.Tick += (_, _) => { preferencesTimer.Stop(); SaveCurrentPreferences(); };
        recoveryTimer.Tick += (_, _) => TryRecoverRoute();

        FormClosing += (sender, eventArgs) =>
        {
            preferencesTimer.Stop();
            SaveCurrentPreferences();
            if (eventArgs.CloseReason == CloseReason.UserClosing && !isSystemExiting)
            {
                eventArgs.Cancel = true;
                trayManager.MinimizeToTray();
            }
            else StopRelay();
        };

        AcceptButton = masterToggleButton;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            StopRelay();
            telemetryTimer.Dispose(); preferencesTimer.Dispose(); recoveryTimer.Dispose();
            updateTimer.Dispose(); appUpdates.Dispose();
            tooltipHelper.Dispose(); trayManager.Dispose(); applicationIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ExitApplication()
    {
        isSystemExiting = true;
        Close();
    }

    private void UpdateUpdateFeedback()
    {
        if (IsDisposed) return;
        UiText.Bind(updatesButton, appUpdates.ReadyToRestart ? "安装更新" : "软件更新");
        tooltipHelper.SetToolTip(updatesButton, appUpdates.StatusText);
        trayManager.SetUpdateReady(appUpdates.ReadyToRestart);
    }

    private void ShowUpdates()
    {
        using var dialog = new UpdateForm(appUpdates) { Icon = Icon };
        if (dialog.ShowDialog(this) != DialogResult.OK || !appUpdates.ReadyToRestart) return;
        SaveCurrentPreferences();
        if (preferencesSaveFailed)
        {
            MessageBox.Show(this, UiText.T("设置尚未保存，更新已取消。请检查用户数据目录是否可写。"), UiText.T("软件更新"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { appUpdates.RestartAfterExit(); ExitApplication(); }
        catch (Exception error) { MessageBox.Show(this, error.Message, UiText.T("软件更新"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ToggleMonitoring()
    {
        if (activeRelay != null || recoveryRoute != null) StopRelay();
        else StartRelay();
    }

    private void ToggleMute()
    {
        gainSlider.Value = gainSlider.Value > 0 ? 0 : preMuteGain;
    }

    private void RefreshProfiles(string? selectedName = null)
    {
        refreshingProfiles = true;
        try
        {
            profileSelector.Items.Clear();
            profileSelector.Items.Add(UiText.T("当前配置"));
            profileSelector.Items.AddRange(profiles.Cast<object>().ToArray());
            var selected = profiles.FirstOrDefault(profile => string.Equals(profile.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (selected == null) profileSelector.SelectedIndex = 0;
            else profileSelector.SelectedItem = selected;
            trayManager.UpdateProfiles(profiles, selected);
        }
        finally { refreshingProfiles = false; }
        UpdateProfileActions();
    }

    private void UpdateProfileActions()
    {
        saveProfileButton.Enabled = !isLoadingDevices && CanMonitor(sourceSelector.SelectedItem as Endpoint,
            targetSelector.SelectedItem as Endpoint, channelSelector.SelectedIndex * 2);
        deleteProfileButton.Enabled = profileSelector.SelectedItem is ListeningProfile;
    }

    private void MarkProfileChanged()
    {
        if (applyingProfile || isLoadingDevices || refreshingProfiles) return;
        if (profileSelector.SelectedItem is ListeningProfile) RefreshProfiles();
        else UpdateProfileActions();
    }

    private bool SaveProfile(string name)
    {
        if (!ListeningProfile.IsValidName(name))
        {
            SetStatusText("方案名称无效", "请输入 1 到 24 个字符，名称不能包含换行等控制字符。", "需处理", Theme.Amber);
            return false;
        }
        if (sourceSelector.SelectedItem is not Endpoint source || targetSelector.SelectedItem is not Endpoint target
            || !CanMonitor(source, target, channelSelector.SelectedIndex * 2))
        {
            SetStatusText("当前配置无法保存", "确认设备可用并选择有效声道后，再保存方案。", "需处理", Theme.Amber);
            return false;
        }
        name = name.Trim();
        var profile = new ListeningProfile(name, source.Id, target.Id, channelSelector.SelectedIndex * 2,
            latencySelector.SelectedIndex switch { 0 => 60, 2 => 200, _ => 100 }, gainSlider.Value, preMuteGain);
        int existing = profiles.FindIndex(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing < 0) profiles.Add(profile);
        else profiles[existing] = profile;
        profileNameHint = name;
        RefreshProfiles(name);
        QueuePreferencesSave();
        SetStatusText("方案已保存", "“{0}”已保存，可从场景方案或托盘菜单切换。", "已保存", Theme.Blue, name);
        return true;
    }

    private bool DeleteProfile(string name)
    {
        int existing = profiles.FindIndex(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing < 0) return false;
        profiles.RemoveAt(existing);
        profileNameHint = null;
        RefreshProfiles();
        QueuePreferencesSave();
        return true;
    }

    private void ApplyProfile(ListeningProfile profile)
    {
        if (!profile.IsValid)
        {
            SetStatusText("方案配置无效", "此方案的设备或参数无效，请重新保存一个有效方案。", "需处理", Theme.Amber);
            return;
        }
        applyingProfile = true;
        try
        {
            StopRelay();
            deviceVolumes[profile.Target] = profile.Volume;
            deviceUnmuteVolumes[profile.Target] = profile.UnmuteVolume;
            bool loaded = LoadDevices(profile: profile);
            latencySelector.SelectedIndex = profile.Latency switch { 60 => 0, 200 => 2, _ => 1 };
            gainTargetId = profile.Target;
            preMuteGain = profile.UnmuteVolume;
            gainSlider.Value = profile.Volume;
            UpdateGainState();
            RememberTargetVolume();
            profileNameHint = profile.Name;
            RefreshProfiles(profile.Name);
            if (loaded)
            {
                RefreshIdleState();
                if (CanMonitor(sourceSelector.SelectedItem as Endpoint, targetSelector.SelectedItem as Endpoint, channelSelector.SelectedIndex * 2))
                    SetStatusText("方案已应用", "“{0}”已就绪，确认输出设备后点击开始监听。", "就绪", Theme.Blue, profile.Name);
                else if (sourceSelector.SelectedItem != null && targetSelector.SelectedItem != null)
                    SetStatusText("方案声道不可用", "此设备已不支持保存的声道，请在高级设置中重新选择。", "需处理", Theme.Amber);
            }
            QueuePreferencesSave();
        }
        finally { applyingProfile = false; }
    }

    internal static bool CanMonitor(Endpoint? source, Endpoint? target, int channelOffset) =>
        source is { IsVirtual: true } && target is { IsVirtual: false } && source.Id != target.Id
        && channelOffset >= 0 && channelOffset % 2 == 0
        && (source.Channels == 1 ? channelOffset == 0 : channelOffset + 1 < source.Channels);

    internal static bool IsDeviceUnavailableError(Exception error) => error.HResult is
        (int)AudioClientErrorCode.DeviceInvalidated or (int)AudioClientErrorCode.ResourcesInvalidated;

    internal static bool CanRetryRecovery(Exception error) => IsDeviceUnavailableError(error)
        || error.HResult is (int)AudioClientErrorCode.DeviceInUse or (int)AudioClientErrorCode.ServiceNotRunning;

    private void StartRelay(bool recovering = false)
    {
        if (sourceSelector.SelectedItem is not Endpoint inputEndpoint || targetSelector.SelectedItem is not Endpoint outputEndpoint) return;
        int channel = channelSelector.SelectedIndex * 2;
        if (!CanMonitor(inputEndpoint, outputEndpoint, channel)) return;
        int targetLatency = latencySelector.SelectedIndex switch { 0 => 60, 2 => 200, _ => 100 };
        var route = new ListeningRoute(inputEndpoint, outputEndpoint, channel, targetLatency);
        try
        {
            var newRelay = new AudioRelay();
            activeRelay = newRelay;
            connectedRoute = route;
            newRelay.Failed += error => HandleRelayFailure(newRelay, error);

            newRelay.Start(inputEndpoint, outputEndpoint, channel, targetLatency, gainSlider.Value / 1000f);

            recoveryRoute = null;
            recoveryTimer.Stop();
            lastAudioError = null;
            lastSignalTime = DateTime.UtcNow;
            hasReceivedSignal = false;
            SetEngineState(true);
            telemetryTimer.Start();
            SaveCurrentPreferences();
            UpdateTelemetry();
        }
        catch (Exception streamException)
        {
            StopRelay();
            LoadDevices();
            lastAudioError = streamException.Message;
            if (recovering && CanRetryRecovery(streamException))
            {
                BeginRecovery(route, "原设备已连接，暂时无法打开。每 5 秒重试，可点击停止等待取消。", 5000);
                return;
            }
            string advice = streamException.HResult == unchecked((int)0x8889000A)
                ? "请与网页选择同一个 CABLE 输入；关闭其他软件的独占模式后再开始。"
                : "确认设备已连接，点击刷新设备，再检查网页与这里的 CABLE 名称一致。";
            SetStatusText("无法开始监听", advice, "需处理", Theme.Red);
            tooltipHelper.SetToolTip(statusMessage, streamException.Message);
        }
    }

    private void HandleRelayFailure(AudioRelay failedRelay, Exception relayException)
    {
        if (isSystemExiting || IsDisposed || !IsHandleCreated) return;
        void ShowFailure()
        {
            if (isSystemExiting || !ReferenceEquals(activeRelay, failedRelay)) return;
            var interruptedRoute = connectedRoute;
            bool disconnected = IsDeviceUnavailableError(relayException);
            try { disconnected |= !failedRelay.DevicesAvailable; }
            catch (Exception stateException) { disconnected |= IsDeviceUnavailableError(stateException); }
            StopRelay();
            LoadDevices();
            lastAudioError = relayException.Message;
            if (disconnected && interruptedRoute != null)
            {
                BeginRecovery(interruptedRoute, "原设备重新连接后会自动恢复监听；点击停止等待可取消。");
                return;
            }
            SetStatusText("监听已中断", "请重新连接设备，点击刷新设备后，再开始监听。", "需处理", Theme.Amber);
            tooltipHelper.SetToolTip(statusMessage, relayException.Message);
        }
        try
        {
            if (InvokeRequired) BeginInvoke((Action)ShowFailure);
            else ShowFailure();
        }
        catch (InvalidOperationException) when (IsDisposed || !IsHandleCreated || isSystemExiting) { }
    }

    private void StopRelay()
    {
        recoveryTimer.Stop();
        recoveryRoute = null;
        connectedRoute = null;
        telemetryTimer.Stop();
        var relayToDispose = activeRelay;
        activeRelay = null;
        try { relayToDispose?.Dispose(); }
        catch (Exception stopException) { tooltipHelper.SetToolTip(statusMessage, stopException.Message); }
        finally
        {
            stereoMeter.SetStereoLevels(0f, 0f);
            decibelLabel.Text = "-inf dBFS";
            latencyStatLabel.Text = "0 ms";
            underrunLabel.Text = UiText.T("0 次");
            SetEngineState(false);
            RefreshIdleState();
        }
    }

    private void SetEngineState(bool isRunning)
    {
        bool recovering = recoveryRoute != null;
        sourceSelector.Enabled = targetSelector.Enabled = channelSelector.Enabled = latencySelector.Enabled = !isRunning && !recovering;
        refreshButton.Enabled = !isRunning;
        UiText.Bind(masterToggleButton, recovering ? "停止等待" : isRunning ? "停止监听" : "开始监听");
        masterToggleButton.Enabled = isRunning || recovering || CanMonitor(sourceSelector.SelectedItem as Endpoint, targetSelector.SelectedItem as Endpoint, channelSelector.SelectedIndex * 2);
        masterToggleButton.Danger = isRunning || recovering;
        masterToggleButton.Primary = !isRunning && !recovering;
        masterToggleButton.Invalidate();
        trayManager.UpdateMenuStates(isRunning, gainSlider.Value == 0, recovering);
        UpdateProfileActions();
    }

    private void BeginRecovery(ListeningRoute route, string explanation, int interval = 2000)
    {
        recoveryRoute = route;
        recoveryTimer.Interval = interval;
        recoveryTimer.Start();
        SetEngineState(false);
        SetStatusText("等待恢复监听", explanation, "等待设备", Theme.Amber);
    }

    private void TryRecoverRoute()
    {
        var route = recoveryRoute;
        if (route == null || activeRelay != null || isSystemExiting) return;
        try
        {
            var endpoints = Endpoint.List();
            var source = endpoints.FirstOrDefault(endpoint => endpoint.Id == route.Source.Id);
            var target = endpoints.FirstOrDefault(endpoint => endpoint.Id == route.Target.Id);
            if (source == null || target == null) return;
            if (!CanMonitor(source, target, route.Channel))
            {
                StopRelay();
                LoadDevices(endpoints);
                SetStatusText("原声道不可用", "设备格式发生变化，请重新选择监听声道后开始。", "需处理", Theme.Amber);
                return;
            }
            LoadDevices(endpoints);
            sourceSelector.SelectedItem = source;
            targetSelector.SelectedItem = target;
            channelSelector.SelectedIndex = route.Channel / 2;
            latencySelector.SelectedIndex = route.Latency switch { 60 => 0, 200 => 2, _ => 1 };
            RestoreTargetVolume();
            StartRelay(recovering: true);
        }
        catch (Exception recoveryException)
        {
            lastAudioError = recoveryException.Message;
            if (!CanRetryRecovery(recoveryException))
            {
                StopRelay();
                SetStatusText("无法恢复连接", "请点击检查音频，确认设备和 Windows 音频服务后重新开始。", "需处理", Theme.Red);
                return;
            }
            recoveryTimer.Interval = 5000;
            SetStatusText("等待音频服务恢复", "每 5 秒重试原连接；点击停止等待可取消。", "等待恢复", Theme.Amber);
        }
    }

    private void ShowDiagnostics()
    {
        var report = AudioDiagnostics.Run(sourceSelector.SelectedItem as Endpoint ?? recoveryRoute?.Source,
            targetSelector.SelectedItem as Endpoint ?? recoveryRoute?.Target,
            recoveryRoute?.Channel ?? channelSelector.SelectedIndex * 2, gainSlider.Value, activeRelay != null, lastAudioError);
        using var dialog = new DiagnosticsForm(report) { Icon = Icon };
        dialog.ShowDialog(this);
    }

    private void RestoreTargetVolume()
    {
        if (targetSelector.SelectedItem is not Endpoint target || gainTargetId == target.Id) return;
        gainTargetId = target.Id;
        preMuteGain = currentPreferences!.TargetUnmuteVolume(target.Id);
        gainSlider.Value = currentPreferences.TargetVolume(target.Id);
        UpdateGainState();
        RememberTargetVolume();
    }

    private void RememberTargetVolume()
    {
        if (gainTargetId == null) return;
        deviceVolumes[gainTargetId] = gainSlider.Value;
        deviceUnmuteVolumes[gainTargetId] = preMuteGain;
    }

    private void UpdateTelemetry()
    {
        var relay = activeRelay;
        if (relay == null) return;
        try
        {
        if (!relay.DevicesAvailable) throw new InvalidOperationException(UiText.T("音频设备已断开。"));
        float peakL = relay.GetLeftPeak();
        float peakR = relay.GetRightPeak();
        stereoMeter.SetStereoLevels(peakL, peakR);

        float maxPeak = Math.Max(peakL, peakR);
        decibelLabel.Text = maxPeak < 0.0001f ? "-inf dBFS" : UiText.T("{0:0.0} dBFS", 20 * Math.Log10(maxPeak));
        latencyStatLabel.Text = UiText.T("{0:0} ms", relay.BufferedMilliseconds);
        underrunLabel.Text = UiText.T("{0} 次", relay.UnderrunCount);
        underrunLabel.ForeColor = relay.UnderrunCount > 0 ? Theme.Amber : Theme.Green;

        if (maxPeak >= 0.0001f) { lastSignalTime = DateTime.UtcNow; hasReceivedSignal = true; }

        if (relay.OutputMuted) SetStatusText("扬声器静音", "请在系统任务栏解除耳机静音", "需处理", Theme.Amber);
        else if (relay.SourceMuted) SetStatusText("虚拟线路静音", "请核查网页播放音量并取消静音", "需处理", Theme.Amber);
        else if (gainSlider.Value == 0) SetStatusText("输出已静音", "请推高右侧增益推子即可听到声音", "已静音", Theme.Amber);
        else if ((DateTime.UtcNow - lastSignalTime).TotalSeconds >= 2)
            SetStatusText("等待网页声音", "播放网页声音，并确认网页扬声器与声音来源选择同一个 CABLE。", "等待声音", Theme.Blue);
        else if (!hasReceivedSignal)
            SetStatusText("监听已连接", "线路已连接，开始播放网页声音即可。", "已连接", Theme.Blue);
        else SetStatusText("实时监听中", "声音已稳定转发至耳机，保持后台运行", "中继中", Theme.Green);
        }
        catch (Exception relayException) { HandleRelayFailure(relay, relayException); }
    }

    private void SetStatusText(string headline, string explanation, string badgeLabel, Color toneColor, params object?[] arguments)
    {
        UiText.Bind(statusTitle, headline);
        statusExplanation = explanation;
        statusArguments = arguments;
        statusBadge = badgeLabel;
        UpdateSaveFeedback();
        tooltipHelper.SetToolTip(statusMessage, null);
        engineBadge.Text = "● " + UiText.T(badgeLabel);
        engineBadge.AccentColor = toneColor;
    }

    private void UpdateGainState()
    {
        gainLabel.Text = UiText.T("{0:0.0}%", gainSlider.Value / 10f);
        if (gainSlider.Value > 0) preMuteGain = gainSlider.Value;
        UiText.Bind(muteButton, gainSlider.Value == 0 ? "取消静音" : "静音");
        muteButton.Danger = gainSlider.Value == 0;
        muteButton.Invalidate();
        trayManager.UpdateMenuStates(activeRelay != null, gainSlider.Value == 0, recoveryRoute != null);
    }

    private void UpdateSaveFeedback() => statusMessage.Text = UiText.T(statusExplanation, statusArguments)
        + (preferencesSaveFailed ? UiText.T("\n设置未能保存，请检查设置目录是否可写。") : "");

    private void QueuePreferencesSave()
    {
        if (isLoadingDevices || !persistPreferences || isSystemExiting) return;
        preferencesTimer.Stop();
        preferencesTimer.Start();
    }

    private void SaveCurrentPreferences()
    {
        if (isLoadingDevices || !persistPreferences) return;
        currentPreferences = new Preferences(
            (sourceSelector.SelectedItem as Endpoint)?.Id ?? currentPreferences?.Source ?? "",
            (targetSelector.SelectedItem as Endpoint)?.Id ?? currentPreferences?.Target ?? "",
            channelSelector.SelectedIndex >= 0 ? channelSelector.SelectedIndex * 2 : currentPreferences?.Channel ?? 0,
            latencySelector.SelectedIndex switch { 0 => 60, 2 => 200, _ => 100 },
            gainSlider.Value, Theme.IsDark, Version: 2, DeviceVolumes: deviceVolumes,
            DeviceUnmuteVolumes: deviceUnmuteVolumes, UnmuteVolume: preMuteGain, Profiles: profiles, Language: UiText.Language);
        preferencesSaveFailed = !PreferencesStorage.SavePreferences(currentPreferences);
        UpdateSaveFeedback();
    }

    private void RefreshIdleState()
    {
        if (isLoadingDevices || activeRelay != null || recoveryRoute != null) return;
        masterToggleButton.Enabled = CanMonitor(sourceSelector.SelectedItem as Endpoint, targetSelector.SelectedItem as Endpoint, channelSelector.SelectedIndex * 2);
        if (sourceSelector.SelectedItem == null)
            SetStatusText("声音来源不可用", sourceSelector.Items.Count == 0 ? "未找到 VB-CABLE。确认已安装并启用后，点击刷新设备。" : "请选择与网页扬声器同名的 CABLE；重新连接设备后可点击刷新。", "需处理", Theme.Amber);
        else if (targetSelector.SelectedItem == null)
            SetStatusText("监听设备不可用", "连接耳机或扬声器后点击刷新设备，或重新选择一个真实输出设备。", "需处理", Theme.Amber);
        else if (channelSelector.SelectedIndex < 0)
            SetStatusText("监听声道不可用", "请展开高级设置，重新选择此设备支持的监听声道。", "需处理", Theme.Amber);
        else if (gainSlider.Value == 0)
            SetStatusText("监听音量已静音", "可开始监听；需要听到声音时，请调高音量或点击取消静音。", "已静音", Theme.Amber);
        else SetStatusText("准备就绪", "确认网页选择同一个 CABLE，然后点击开始监听。", "就绪", Theme.Blue);
    }

    private void ApplyCurrentTheme()
    {
        Theme.ApplyTheme(this);
        gainLabel.ForeColor = latencyStatLabel.ForeColor = Theme.Blue;
        underrunLabel.ForeColor = Theme.Green;
    }

    private void ToggleTheme()
    {
        Theme.SetDark(!Theme.IsDark);
        UiText.Bind(themeToggleButton, Theme.IsDark ? "浅色模式" : "深色模式");
        ApplyCurrentTheme();
        QueuePreferencesSave();
    }

    private bool LoadDevices(List<Endpoint>? availableEndpoints = null, ListeningProfile? profile = null)
    {
        preferencesTimer.Stop();
        SaveCurrentPreferences();
        if (profile != null) currentPreferences = currentPreferences! with
            { Source = profile.Source, Target = profile.Target, Channel = profile.Channel, Latency = profile.Latency, Volume = profile.Volume, Version = 2, UnmuteVolume = profile.UnmuteVolume };
        string? preservedSource = profile?.Source ?? (sourceSelector.SelectedItem as Endpoint)?.Id ?? currentPreferences?.Source;
        string? preservedTarget = profile?.Target ?? (targetSelector.SelectedItem as Endpoint)?.Id ?? currentPreferences?.Target;
        isLoadingDevices = true;
        sourceSelector.Items.Clear();
        targetSelector.Items.Clear();
        try
        {
        var endpointsList = availableEndpoints ?? Endpoint.List();
        sourceSelector.Items.AddRange(endpointsList.Where(e => e.IsVirtual).Cast<object>().ToArray());
        targetSelector.Items.AddRange(endpointsList.Where(e => !e.IsVirtual).Cast<object>().ToArray());

        var selectedInput = !string.IsNullOrEmpty(preservedSource)
            ? endpointsList.FirstOrDefault(e => e.Id == preservedSource && e.IsVirtual)
            : endpointsList.FirstOrDefault(e => e.Name.Contains("CABLE In 16ch", StringComparison.OrdinalIgnoreCase))
                ?? endpointsList.FirstOrDefault(e => e.IsVirtual);
        var selectedOutput = !string.IsNullOrEmpty(preservedTarget)
            ? endpointsList.FirstOrDefault(e => e.Id == preservedTarget && !e.IsVirtual)
            : Endpoint.PreferredOutput(endpointsList);

        sourceSelector.SelectedItem = selectedInput;
        targetSelector.SelectedItem = selectedOutput;

        if (profile != null)
        {
            channelSelector.SelectedIndex = selectedInput != null && CanMonitor(selectedInput, selectedOutput, profile.Channel)
                ? profile.Channel / 2 : -1;
        }
        else if (currentPreferences != null && selectedInput?.Id == currentPreferences.Source)
        {
            channelSelector.SelectedIndex = CanMonitor(selectedInput, selectedOutput, currentPreferences.Channel)
                ? currentPreferences.Channel / 2 : -1;
        }

        }
        catch (Exception deviceException)
        {
            masterToggleButton.Enabled = recoveryRoute != null;
            SetStatusText("读取设备失败", "请确认 Windows 音频服务和设备可用，再点击刷新设备。", "需处理", Theme.Red);
            tooltipHelper.SetToolTip(statusMessage, deviceException.Message);
            return false;
        }
        finally { isLoadingDevices = false; }
        RestoreTargetVolume();
        UpdateDeviceDetails();
        RefreshIdleState();
        UpdateProfileActions();
        QueuePreferencesSave();
        return true;
    }

    private void RefreshChannels()
    {
        channelSelector.Items.Clear();
        if (sourceSelector.SelectedItem is not Endpoint endpoint) return;
        if (endpoint.Channels == 1) channelSelector.Items.Add(UiText.T("单声道 → 双耳"));
        else
        {
            for (int ch = 0; ch + 1 < endpoint.Channels; ch += 2)
                channelSelector.Items.Add(UiText.T("声道 {0} / {1}", ch + 1, ch + 2) + (ch == 0 ? UiText.T(" · 推荐") : ""));
        }
        if (channelSelector.Items.Count > 0) channelSelector.SelectedIndex = 0;
    }

    private void UpdateDeviceDetails()
    {
        sourceInfo.Text = sourceSelector.SelectedItem is Endpoint inDev ? UiText.T("{0:0.#} kHz · {1} 声道", inDev.SampleRate / 1000d, inDev.Channels) : UiText.T("与网页选项一致");
        targetInfo.Text = targetSelector.SelectedItem is Endpoint outDev ? UiText.T("{0:0.#} kHz · 物理输出", outDev.SampleRate / 1000d) : UiText.T("真实物理输出设备");
        tooltipHelper.SetToolTip(sourceSelector, sourceSelector.SelectedItem?.ToString());
        tooltipHelper.SetToolTip(targetSelector, targetSelector.SelectedItem?.ToString());
    }

    private void ChangeLanguage(string language)
    {
        isLoadingDevices = refreshingProfiles = true;
        SuspendLayout();
        try
        {
            UiText.SetLanguage(language);
            UiText.Apply(this);
            for (int index = 0; index < channelSelector.Items.Count; index++)
                channelSelector.Items[index] = sourceSelector.SelectedItem is Endpoint { Channels: 1 }
                    ? UiText.T("单声道 → 双耳")
                    : UiText.T("声道 {0} / {1}", index * 2 + 1, index * 2 + 2) + (index == 0 ? UiText.T(" · 推荐") : "");
            string[] buffers = ["60 毫秒 · 低延迟", "100 毫秒 · 平衡", "200 毫秒 · 稳定"];
            for (int index = 0; index < buffers.Length; index++) latencySelector.Items[index] = UiText.T(buffers[index]);
            if (profileSelector.Items.Count > 0) profileSelector.Items[0] = UiText.T("当前配置");
            UpdateDeviceDetails();
            UpdateTooltips();
            UpdateSaveFeedback();
            engineBadge.Text = "● " + UiText.T(statusBadge);
            underrunLabel.Text = UiText.T("{0} 次", activeRelay?.UnderrunCount ?? 0);
            trayManager.UpdateLanguage();
            UpdateUpdateFeedback();
            trayManager.UpdateProfiles(profiles, profileSelector.SelectedItem as ListeningProfile);
            SetEngineState(activeRelay != null);
            UpdateGainState();
        }
        finally
        {
            isLoadingDevices = refreshingProfiles = false;
            ResumeLayout(true);
        }
        UpdateProfileActions();
        QueuePreferencesSave();
    }
}
