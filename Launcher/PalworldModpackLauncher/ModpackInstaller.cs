using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace PalworldModpackLauncher;

internal sealed class ModpackInstaller
{
    private static readonly string Win64RelativePath = Path.Combine("Pal", "Binaries", "Win64");
    private static readonly string StateRelativePath = Path.Combine("ue4ss", "palworld-modpack-state.json");
    private static readonly string MetadataEntryName = "modpack-package.json";

    public InstalledState? ReadState(string gameRoot)
    {
        try
        {
            var root = PalworldLocator.Resolve(gameRoot);
            if (root is null) return null;
            var path = Path.Combine(root, Win64RelativePath, StateRelativePath);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<InstalledState>(File.ReadAllText(path), JsonOptions)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public OperationResult ApplyPackage(
        string gameRoot,
        string packagePath,
        string expectedSha256,
        Version version,
        string releaseTag)
    {
        var createdFiles = new List<string>();
        string? backupRoot = null;
        try
        {
            var win64 = ValidateGameRoot(gameRoot);
            ValidateHash(packagePath, expectedSha256);

            using var archive = ZipFile.OpenRead(packagePath);
            var metadataEntry = archive.Entries.FirstOrDefault(entry =>
                Normalize(entry.FullName).Equals(MetadataEntryName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"O pacote não contém {MetadataEntryName}.");
            var metadata = ReadMetadata(metadataEntry);
            if (!Version.TryParse(metadata.Version, out var packageVersion) || packageVersion != version)
                throw new InvalidDataException("A versão interna do pacote não corresponde à Release.");

            var entries = archive.Entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name))
                .Where(entry => !Normalize(entry.FullName).Equals(MetadataEntryName, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(entry => Normalize(entry.FullName), StringComparer.OrdinalIgnoreCase);
            var managedFiles = metadata.Files.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (managedFiles.Length == 0 || managedFiles.Any(file => !entries.ContainsKey(file)))
                throw new InvalidDataException("A lista de arquivos do pacote está incompleta.");

            ValidateRequiredFiles(managedFiles, packageVersion);
            var previous = ReadState(gameRoot);
            backupRoot = Path.Combine(win64, "Palworld-Modpack-Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            var touchedFiles = managedFiles
                .Concat(previous?.ManagedFiles ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var relative in touchedFiles)
            {
                var destination = SafeCombine(win64, relative);
                if (!File.Exists(destination))
                {
                    if (entries.ContainsKey(relative)) createdFiles.Add(relative);
                    continue;
                }

                var backup = SafeCombine(backupRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(destination, backup, overwrite: true);
            }

            foreach (var stale in (previous?.ManagedFiles ?? Array.Empty<string>())
                         .Except(managedFiles, StringComparer.OrdinalIgnoreCase))
            {
                var stalePath = SafeCombine(win64, stale);
                if (File.Exists(stalePath)) File.Delete(stalePath);
            }

            foreach (var relative in managedFiles)
            {
                var destination = SafeCombine(win64, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var source = entries[relative].Open();
                using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(target);
            }

            var state = new InstalledState(
                version.ToString(3),
                releaseTag,
                DateTimeOffset.Now,
                managedFiles,
                Directory.Exists(backupRoot) ? backupRoot : null);
            var statePath = SafeCombine(win64, StateRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));

            return new OperationResult(true, $"Modpack {version.ToString(3)} instalado com sucesso.");
        }
        catch (Exception exception)
        {
            TryRollback(gameRoot, backupRoot, createdFiles);
            return new OperationResult(false, "Não foi possível atualizar o modpack:\n" + exception.Message);
        }
    }

    private static PackageMetadata ReadMetadata(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<PackageMetadata>(stream, JsonOptions)
            ?? throw new InvalidDataException("Os metadados do pacote são inválidos.");
    }

    private static void ValidateRequiredFiles(IEnumerable<string> files, Version packageVersion)
    {
        var set = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>
        {
            "dwmapi.dll",
            "ue4ss/UE4SS.dll",
            "ue4ss/Mods/HoverTransfer/enabled.txt",
            "ue4ss/Mods/HoverTransfer/Scripts/main.lua",
            "ue4ss/Mods/HoverTransfer/Scripts/HoverTransferKeys.dll",
        };
        if (packageVersion >= new Version(0, 4, 0))
        {
            required.AddRange(new[]
            {
                "ue4ss/Mods/AltTabWorkContinuation/enabled.txt",
                "ue4ss/Mods/AltTabWorkContinuation/Scripts/main.lua",
                "ue4ss/Mods/AltTabWorkContinuation/Scripts/continuation_policy.lua",
                "ue4ss/Mods/AltTabWorkContinuation/Scripts/AltTabWorkContinuationFocus.dll",
            });
        }
        var missing = required.Where(file => !set.Contains(file)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException("Arquivos obrigatórios ausentes: " + string.Join(", ", missing));
    }

    private static void ValidateHash(string path, string expected)
    {
        var normalized = expected.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) normalized = normalized[7..];
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A verificação SHA-256 do pacote falhou.");
    }

    private static string ValidateGameRoot(string gameRoot)
    {
        var resolved = PalworldLocator.Resolve(gameRoot)
            ?? throw new DirectoryNotFoundException("A instalação do Palworld não foi encontrada.");
        return Path.GetFullPath(Path.Combine(resolved, Win64RelativePath));
    }

    private static string SafeCombine(string root, string relative)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Caminho inseguro no pacote: " + relative);
        return combined;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static void TryRollback(string gameRoot, string? backupRoot, IEnumerable<string> createdFiles)
    {
        try
        {
            var resolved = PalworldLocator.Resolve(gameRoot);
            if (resolved is null) return;
            var win64 = Path.Combine(resolved, Win64RelativePath);
            foreach (var relative in createdFiles)
            {
                var path = SafeCombine(win64, relative);
                if (File.Exists(path)) File.Delete(path);
            }
            if (string.IsNullOrWhiteSpace(backupRoot) || !Directory.Exists(backupRoot)) return;
            foreach (var backup in Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(backupRoot, backup);
                var destination = SafeCombine(win64, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(backup, destination, overwrite: true);
            }
        }
        catch
        {
            // Mantém o erro original da atualização.
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
