using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;

namespace VBAudioMonitor;

internal sealed class SignedUpdateManager(IVelopackLocator locator, string? feedDirectory = null)
    : UpdateManager(new SignedUpdateSource(feedDirectory), locator: locator)
{
    public override VelopackAsset? UpdatePendingRestart
    {
        get
        {
            try
            {
                var asset = base.UpdatePendingRestart;
                if (asset == null) return null;
                VerifyCachedPackage(asset);
                return asset;
            }
            catch (IOException) { return null; }
            catch (InvalidDataException) { return null; }
            catch (CryptographicException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }

    internal void VerifyCachedPackage(VelopackAsset asset)
    {
        SignedUpdateSource.ValidateIdentity(asset);
        string path = Path.Combine(Locator.PackagesDir ?? throw new InvalidOperationException("Update cache is unavailable."), asset.FileName);
        using var proof = new FileStream(path + ".sig", FileMode.Open, FileAccess.Read, FileShare.Read);
        if (proof.Length != 64) throw new InvalidDataException("Update signature has an invalid length.");
        byte[] signature = new byte[64];
        proof.ReadExactly(signature);
        if (!UpdateProof.VerifyFile(asset.Version.ToString(), asset.FileName, path, signature))
            throw new CryptographicException("Update publisher signature verification failed.");
    }

    public override async Task DownloadUpdatesAsync(UpdateInfo updates, Action<int>? progress = null, CancellationToken cancelToken = default)
    {
        var asset = updates.TargetFullRelease;
        SignedUpdateSource.ValidateIdentity(asset);
        string path = Path.Combine(Locator.PackagesDir ?? throw new InvalidOperationException("Update cache is unavailable."), asset.FileName);
        if (File.Exists(path))
        {
            try { VerifyCachedPackage(asset); }
            catch (Exception error) when (error is IOException or InvalidDataException or CryptographicException or UnauthorizedAccessException)
            {
                File.Delete(path);
                File.Delete(path + ".sig");
            }
        }
        // Only signed full packages may reach Velopack's native extraction; do not apply unsigned deltas.
        await base.DownloadUpdatesAsync(new UpdateInfo(asset, updates.IsDowngrade), progress, cancelToken);
        VerifyCachedPackage(asset);
    }
}

internal sealed class SignedUpdateSource : IUpdateSource
{
    private static readonly HttpClient Http = new();
    private readonly string? feedDirectory;
    private readonly IUpdateSource source;

    internal SignedUpdateSource(string? feedDirectory = null)
    {
        this.feedDirectory = feedDirectory;
        source = feedDirectory == null ? new GithubSource(AppUpdates.RepositoryUrl, null, false)
            : new SimpleFileSource(new DirectoryInfo(feedDirectory));
    }

    internal static void ValidateIdentity(VelopackAsset asset)
    {
        if (asset.PackageId != "VBAudioMonitor" || asset.Type != VelopackAssetType.Full || asset.Version == null
            || !Regex.IsMatch(asset.Version.ToString(), @"^\d+\.\d+\.\d+$")
            || asset.FileName != $"VBAudioMonitor-{asset.Version}-full.nupkg")
            throw new InvalidDataException("Update package identity or filename is invalid.");
    }

    public Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel,
        Guid? stagingId = null, VelopackAsset? latestLocalRelease = null) =>
        source.GetReleaseFeed(logger, appId, channel, stagingId, latestLocalRelease);

    public async Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset asset, string localFile,
        Action<int> progress, CancellationToken cancelToken)
    {
        ValidateIdentity(asset);
        if (asset.Size <= 0 || asset.Size > 1024L * 1024 * 1024
            || !Regex.IsMatch(asset.SHA256 ?? "", @"\A[0-9a-fA-F]{64}\z"))
            throw new InvalidDataException("Update size or SHA-256 is invalid.");
        byte[] signature = await ReadSignatureAsync(asset, cancelToken);
        if (!UpdateProof.Verify(asset.Version.ToString(), asset.FileName, asset.Size, asset.SHA256!, signature, UpdateProof.PublicKey))
            throw new CryptographicException("Update metadata is not signed by the pinned publisher.");
        await source.DownloadReleaseEntry(logger, asset, localFile, progress, cancelToken);
        // Verify before returning: Velopack extracts Update.exe immediately after this method.
        if (!UpdateProof.VerifyFile(asset.Version.ToString(), asset.FileName, localFile, signature))
        {
            File.Delete(localFile);
            throw new CryptographicException("Downloaded update differs from its publisher signature.");
        }
        string completePath = localFile.EndsWith(".partial", StringComparison.Ordinal) ? localFile[..^8] : localFile;
        File.WriteAllBytes(completePath + ".sig", signature);
    }

    private async Task<byte[]> ReadSignatureAsync(VelopackAsset asset, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = feedDirectory == null
            ? await Http.GetAsync($"{AppUpdates.RepositoryUrl}/releases/download/v{asset.Version}/{Uri.EscapeDataString(asset.FileName + ".sig")}",
                HttpCompletionOption.ResponseHeadersRead, timeout.Token) : null;
        response?.EnsureSuccessStatusCode();
        await using Stream stream = response != null ? await response.Content.ReadAsStreamAsync(timeout.Token)
            : new FileStream(Path.Combine(feedDirectory!, asset.FileName + ".sig"), FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] signature = new byte[64];
        await stream.ReadExactlyAsync(signature, timeout.Token);
        if (await stream.ReadAsync(new byte[1], timeout.Token) != 0) throw new InvalidDataException("Update signature has an invalid length.");
        return signature;
    }
}
