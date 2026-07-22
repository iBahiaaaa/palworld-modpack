using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace HoverTransferInstaller;

internal sealed record InstallResult(bool Success, string Message);

internal sealed class InstallerService
{
    private static readonly string Win64RelativePath = Path.Combine("Pal", "Binaries", "Win64");
    private static readonly string ModRelativePath = Path.Combine("ue4ss", "Mods", "HoverTransfer");

    public bool IsModInstalled(string gameRoot)
    {
        return File.Exists(Path.Combine(gameRoot, Win64RelativePath, ModRelativePath, "enabled.txt"));
    }

    public InstallResult Install(string gameRoot)
    {
        try
        {
            var win64 = ValidateGameRoot(gameRoot);
            using var payload = PayloadArchive.Open();
            PayloadArchive.Validate(payload);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupRoot = Path.Combine(win64, "HoverTransfer-Backups", timestamp);
            var installedFiles = new List<string>();
            var backupFiles = new List<string>();

            foreach (var entry in payload.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
            {
                var relative = PayloadArchive.Normalize(entry.FullName);
                var destination = SafeCombine(win64, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                if (File.Exists(destination) && !ContentMatches(entry, destination))
                {
                    var backup = SafeCombine(backupRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, overwrite: true);
                    backupFiles.Add(relative);
                }

                using var source = entry.Open();
                using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(target);
                installedFiles.Add(relative);
            }

            var manifest = new InstallManifest(
                Product: "HoverTransfer",
                Version: "0.3.0",
                InstalledAt: DateTimeOffset.Now,
                BackupDirectory: backupFiles.Count > 0 ? backupRoot : null,
                InstalledFiles: installedFiles,
                BackedUpFiles: backupFiles);
            var manifestPath = Path.Combine(win64, ModRelativePath, "install-manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

            return new InstallResult(true,
                backupFiles.Count > 0
                    ? $"Hover Transfer instalado. Backup criado em:\n{backupRoot}"
                    : "Hover Transfer instalado. Abra o Palworld e use H sobre os itens.");
        }
        catch (Exception exception)
        {
            return new InstallResult(false, "Falha na instalação:\n" + exception.Message);
        }
    }

    public InstallResult UninstallMod(string gameRoot)
    {
        try
        {
            var win64 = ValidateGameRoot(gameRoot);
            var modDirectory = Path.Combine(win64, ModRelativePath);
            if (!Directory.Exists(modDirectory))
                return new InstallResult(true, "O Hover Transfer já não está instalado.");

            Directory.Delete(modDirectory, recursive: true);
            return new InstallResult(true, "Hover Transfer removido. O UE4SS e os outros mods foram preservados.");
        }
        catch (Exception exception)
        {
            return new InstallResult(false, "Falha na remoção:\n" + exception.Message);
        }
    }

    private static string ValidateGameRoot(string gameRoot)
    {
        var resolved = PalworldLocator.Resolve(gameRoot)
            ?? throw new DirectoryNotFoundException("A instalação do Palworld não foi encontrada.");
        var win64 = Path.GetFullPath(Path.Combine(resolved, Win64RelativePath));
        if (!File.Exists(Path.Combine(win64, "Palworld-Win64-Shipping.exe")))
            throw new FileNotFoundException("Executável do Palworld não encontrado.");
        return win64;
    }

    private static string SafeCombine(string root, string relative)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Caminho inseguro no pacote: " + relative);
        return combined;
    }

    private static bool ContentMatches(ZipArchiveEntry entry, string destination)
    {
        if (entry.Length != new FileInfo(destination).Length) return false;
        using var source = entry.Open();
        using var target = File.OpenRead(destination);
        return SHA256.HashData(source).SequenceEqual(SHA256.HashData(target));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

internal sealed record InstallManifest(
    string Product,
    string Version,
    DateTimeOffset InstalledAt,
    string? BackupDirectory,
    IReadOnlyList<string> InstalledFiles,
    IReadOnlyList<string> BackedUpFiles);
