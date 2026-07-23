namespace PalworldModpackLauncher;

internal sealed class ModpackUninstaller
{
    private static readonly string Win64RelativePath = Path.Combine("Pal", "Binaries", "Win64");
    private static readonly string StateRelativePath = Path.Combine("ue4ss", "palworld-modpack-state.json");
    private static readonly string[] KnownModDirectories =
    {
        Path.Combine("ue4ss", "Mods", "HoverTransfer"),
        Path.Combine("ue4ss", "Mods", "AltTabWorkContinuation"),
        Path.Combine("ue4ss", "Mods", "AccessorySlotsResearch"),
    };

    private readonly ModpackInstaller installer = new();
    private readonly ModActivationManager activationManager = new();

    public bool HasInstalledContent(string gameRoot)
    {
        try
        {
            var win64 = ResolveWin64(gameRoot);
            if (installer.ReadState(gameRoot) is not null) return true;
            if (File.Exists(SafeCombine(win64, ModActivationManager.StoredLoaderRelativePath))) return true;
            return KnownModDirectories.Any(relative => Directory.Exists(SafeCombine(win64, relative)));
        }
        catch
        {
            return false;
        }
    }

    public OperationResult Uninstall(string gameRoot)
    {
        if (GameSessionManager.IsPalworldRunning(gameRoot))
            return new OperationResult(false, "Feche o Palworld antes de remover os mods.");

        try
        {
            var win64 = ResolveWin64(gameRoot);
            var state = installer.ReadState(gameRoot);
            if (!HasInstalledContent(gameRoot))
                return new OperationResult(true, "O modpack já está removido deste cliente.");

            var managedFiles = (state?.ManagedFiles ?? Array.Empty<string>())
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var backupRoot = Path.Combine(
                win64,
                "Palworld-Modpack-Backups",
                "launcher-removal-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));

            BackupManagedContent(win64, backupRoot, managedFiles);

            var activeLoaderWasManaged = managedFiles.Contains(
                ModActivationManager.ActiveLoaderRelativePath,
                StringComparer.OrdinalIgnoreCase);
            var storedLoaderExists = File.Exists(SafeCombine(win64, ModActivationManager.StoredLoaderRelativePath));
            if (activeLoaderWasManaged || storedLoaderExists)
            {
                var vanilla = activationManager.EnsureVanilla(gameRoot, state);
                if (!vanilla.Success) return vanilla;
            }

            foreach (var relative in managedFiles)
            {
                var path = SafeCombine(win64, relative);
                if (File.Exists(path)) File.Delete(path);
            }

            var statePath = SafeCombine(win64, StateRelativePath);
            if (File.Exists(statePath)) File.Delete(statePath);

            foreach (var relative in KnownModDirectories)
            {
                var directory = SafeCombine(win64, relative);
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }

            var internalDirectory = SafeCombine(win64, "Palworld-Modpack");
            if (Directory.Exists(internalDirectory)) Directory.Delete(internalDirectory, recursive: true);

            return new OperationResult(true,
                "Mods removidos com sucesso. Backup criado em:\n" + backupRoot);
        }
        catch (Exception exception)
        {
            return new OperationResult(false, "Não foi possível remover os mods:\n" + exception.Message);
        }
    }

    private static void BackupManagedContent(
        string win64,
        string backupRoot,
        IReadOnlyCollection<string> managedFiles)
    {
        Directory.CreateDirectory(backupRoot);
        foreach (var relative in managedFiles) BackupFile(win64, backupRoot, relative);
        BackupFile(win64, backupRoot, StateRelativePath);

        foreach (var relative in KnownModDirectories.Append("Palworld-Modpack"))
        {
            var directory = SafeCombine(win64, relative);
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                BackupFile(win64, backupRoot, Path.GetRelativePath(win64, file));
            }
        }
    }

    private static void BackupFile(string win64, string backupRoot, string relative)
    {
        var source = SafeCombine(win64, relative);
        if (!File.Exists(source)) return;
        var destination = SafeCombine(backupRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static string ResolveWin64(string gameRoot)
    {
        var resolved = PalworldLocator.Resolve(gameRoot)
            ?? throw new DirectoryNotFoundException("A instalação do Palworld não foi encontrada.");
        return Path.GetFullPath(Path.Combine(resolved, Win64RelativePath));
    }

    private static string SafeCombine(string root, string relative)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(normalizedRoot, Normalize(relative)));
        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Caminho inseguro: " + relative);
        return combined;
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
}
