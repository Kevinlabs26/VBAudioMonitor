namespace VBAudioMonitor;

/// <summary>
/// Application entry point configuring singleton instance and runtime environment.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] startupArguments)
    {
        if (!startupArguments.Contains("--check") && !startupArguments.Contains("--export-icon"))
            Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        ApplicationConfiguration.Initialize();
        UiText.SetLanguage(startupArguments.Contains("--check") ? "zh-CN" : PreferencesStorage.LoadPreferences()?.Language);
        if (startupArguments.Contains("--export-icon")) { Checks.ExportIcon(); return; }

        if (startupArguments.Contains("--check"))
        {
            int feedArgument = Array.IndexOf(startupArguments, "--update-feed");
            ExecuteDiagnosticCheck(startupArguments.Contains("--audio"), feedArgument >= 0 && feedArgument + 1 < startupArguments.Length ? startupArguments[feedArgument + 1] : null);
            return;
        }

        using var processMutex = new Mutex(true, "Local\\VBAudioMonitor", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(UiText.T("VB 音频监听已经在运行中，请查看任务栏或右下角系统托盘。"), UiText.T("程序已在运行"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var updates = new AppUpdates();
            // Hold the single-instance mutex before applying updates so an active relay is never replaced by a second launch.
            if (updates.ReadyToRestart) { updates.RestartAfterExit(); return; }
            using var mainForm = new MonitorForm(updates: updates);
            Application.Run(mainForm);
        }
        catch (Exception runException)
        {
            MessageBox.Show(UiText.T("程序发生未处理异常：\n\n") + runException.Message, UiText.T("运行错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExecuteDiagnosticCheck(bool shouldTestAudioRouting, string? updateFeed)
    {
        try
        {
            Checks.Run(shouldTestAudioRouting, updateFeed);
        }
        catch (Exception checkException)
        {
            File.WriteAllText("verification-error.txt", checkException.ToString());
            Environment.ExitCode = 1;
        }
    }
}
