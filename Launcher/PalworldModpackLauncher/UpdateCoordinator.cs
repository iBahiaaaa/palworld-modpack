namespace PalworldModpackLauncher;

internal sealed class UpdateCoordinator : IDisposable
{
    private readonly GitHubReleaseClient releaseClient = new();
    private readonly ModpackInstaller installer = new();

    public InstalledState? ReadState(string gameRoot) => installer.ReadState(gameRoot);

    public async Task<(UpdateInfo Latest, bool NeedsUpdate)> CheckAsync(
        string gameRoot,
        CancellationToken cancellationToken = default)
    {
        var latest = await releaseClient.GetLatestAsync(cancellationToken);
        var current = installer.ReadState(gameRoot);
        var currentVersion = ParseVersion(current?.Version);
        return (latest, currentVersion is null || latest.Version > currentVersion);
    }

    public async Task<OperationResult> DownloadAndApplyAsync(
        string gameRoot,
        UpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PalworldModpackLauncher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var packagePath = Path.Combine(tempDirectory, GitHubReleaseClient.PackageAssetName);
            var checksumText = await releaseClient.DownloadTextAsync(update.Checksum, cancellationToken);
            await releaseClient.DownloadFileAsync(update.Package, packagePath, progress, cancellationToken);
            return installer.ApplyPackage(gameRoot, packagePath, checksumText, update.Version, update.Tag);
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); }
            catch { }
        }
    }

    private static Version? ParseVersion(string? value) => Version.TryParse(value, out var version) ? version : null;

    public void Dispose() => releaseClient.Dispose();
}
