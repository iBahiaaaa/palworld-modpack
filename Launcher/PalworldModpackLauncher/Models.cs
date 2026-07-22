using System.Text.Json.Serialization;

namespace PalworldModpackLauncher;

internal sealed record OperationResult(bool Success, string Message);

internal sealed record LauncherSettings(string? GameRoot);

internal sealed record PackageMetadata(string Version, IReadOnlyList<string> Files);

internal sealed record InstalledState(
    string Version,
    string ReleaseTag,
    DateTimeOffset InstalledAt,
    IReadOnlyList<string> ManagedFiles,
    string? BackupDirectory);

internal sealed record UpdateInfo(
    Version Version,
    string Tag,
    ReleaseAsset Package,
    ReleaseAsset Checksum);

internal sealed record LauncherUpdateManifest(string Version);

internal sealed record LauncherUpdateInfo(
    Version Version,
    string Tag,
    ReleaseAsset Executable,
    ReleaseAsset Checksum);

internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("prerelease")] bool Prerelease,
    [property: JsonPropertyName("assets")] IReadOnlyList<ReleaseAsset> Assets);

internal sealed record ReleaseAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string? Digest);
