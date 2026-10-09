using Velopack;
using Velopack.Locators;
using Velopack.Logging;

namespace VBAudioMonitor;

internal sealed class AppUpdates : IDisposable
{
    public const string RepositoryUrl = "https://github.com/Kevinlabs26/VBAudioMonitor";
    private readonly UpdateManager manager;
    private readonly CancellationTokenSource cancellation = new();
    private VelopackAsset? pending;
    private bool disposed;
    private string statusKey;
    private object?[] statusArguments;

    public AppUpdates(UpdateManager? manager = null)
    {
        this.manager = manager ?? new SignedUpdateManager(
            VelopackLocator.IsCurrentSet ? VelopackLocator.Current : VelopackLocator.CreateDefaultForPlatform(logger: new NullVelopackLogger()));
        if (CanUpdate) pending = this.manager.UpdatePendingRestart;
        statusKey = pending != null ? "更新 {0} 已准备好，下次启动时自动安装。" : CanUpdate ? "当前版本：{0}" : "请安装安装版以启用自动更新。";
        statusArguments = pending != null ? [pending.Version.ToString()] : CanUpdate ? [Version] : [];
    }

    public bool CanUpdate => manager.IsInstalled && !manager.IsPortable;
    public bool IsBusy { get; private set; }
    public bool ReadyToRestart => pending != null;
    public string Version => manager.CurrentVersion?.ToString() ?? typeof(AppUpdates).Assembly.GetName().Version!.ToString(3);
    public string StatusText => UiText.T(statusKey, statusArguments);
    public event Action? StateChanged;

    private void SetStatus(string key, params object?[] arguments)
    {
        if (disposed) return;
        statusKey = key;
        statusArguments = arguments;
        StateChanged?.Invoke();
    }

    public async Task CheckAndDownloadAsync()
    {
        if (disposed || IsBusy || !CanUpdate || ReadyToRestart) return;
        IsBusy = true;
        SetStatus("正在检查更新…");
        try
        {
            var update = await manager.CheckForUpdatesAsync();
            if (disposed) return;
            if (update == null)
            {
                SetStatus("已是最新版本：{0}", Version);
                return;
            }
            SetStatus("正在下载更新：{0}%", 0);
            var progress = new Progress<int>(value =>
            {
                if (IsBusy && pending == null) SetStatus("正在下载更新：{0}%", Math.Clamp(value, 0, 100));
            });
            await manager.DownloadUpdatesAsync(update, value => ((IProgress<int>)progress).Report(value), cancellation.Token);
            if (disposed) return;
            pending = update.TargetFullRelease;
            SetStatus("更新 {0} 已准备好，下次启动时自动安装。", pending.Version.ToString());
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) { SetStatus("更新失败，可稍后重试。\n{0}", error.Message); }
        finally
        {
            IsBusy = false;
            if (!disposed) StateChanged?.Invoke();
        }
    }

    public void RestartAfterExit()
    {
        if (disposed || pending == null || IsBusy) throw new InvalidOperationException("No verified update is ready.");
        if (manager is SignedUpdateManager signed) signed.VerifyCachedPackage(pending);
        manager.WaitExitThenApplyUpdates(pending, silent: true, restart: true);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StateChanged = null;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
