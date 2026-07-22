using System.Net.Http.Headers;
using System.Text.Json;

namespace PalworldModpackLauncher;

internal sealed class GitHubReleaseClient : IDisposable
{
    public const string Repository = "iBahiaaaa/palworld-modpack";
    public const string PackageAssetName = "palworld-modpack.zip";
    public const string ChecksumAssetName = "palworld-modpack.zip.sha256";

    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45),
    };

    public GitHubReleaseClient()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PalworldModpackLauncher/1.0");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateInfo> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{Repository}/releases/latest";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("O GitHub retornou uma Release inválida.");

        if (release.Draft || release.Prerelease)
            throw new InvalidDataException("A Release mais recente ainda não está publicada como estável.");

        var versionText = release.TagName.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var version))
            throw new InvalidDataException($"Versão inválida na Release: {release.TagName}");

        var package = release.Assets.FirstOrDefault(asset =>
            asset.Name.Equals(PackageAssetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"A Release não contém {PackageAssetName}.");
        var checksum = release.Assets.FirstOrDefault(asset =>
            asset.Name.Equals(ChecksumAssetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"A Release não contém {ChecksumAssetName}.");

        return new UpdateInfo(version, release.TagName, package, checksum);
    }

    public async Task<string> DownloadTextAsync(ReleaseAsset asset, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetStringAsync(asset.BrowserDownloadUrl, cancellationToken);
    }

    public async Task DownloadFileAsync(
        ReleaseAsset asset,
        string destination,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (asset.Size <= 0 || asset.Size > 300 * 1024 * 1024)
            throw new InvalidDataException("O tamanho do pacote informado pelo GitHub é inválido.");

        using var response = await httpClient.GetAsync(
            asset.BrowserDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[128 * 1024];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            progress?.Report((int)Math.Clamp(received * 100L / asset.Size, 0, 100));
        }
    }

    public void Dispose() => httpClient.Dispose();
}
