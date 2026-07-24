using System.Security.Cryptography;

namespace PalworldModpackLauncher;

internal sealed class ModActivationManager
{
    public const string ActiveLoaderRelativePath = "dwmapi.dll";
    public static readonly string StoredLoaderRelativePath =
        Path.Combine("Palworld-Modpack", "loader", "dwmapi.dll");

    private static readonly string Win64RelativePath = Path.Combine("Pal", "Binaries", "Win64");

    public OperationResult EnsureVanilla(string gameRoot, InstalledState? installedState = null)
    {
        try
        {
            var win64 = ResolveWin64(gameRoot);
            var active = Path.Combine(win64, ActiveLoaderRelativePath);
            var stored = Path.Combine(win64, StoredLoaderRelativePath);

            if (!File.Exists(active))
                return new OperationResult(true, "Modo vanilla ativo.");

            if (!File.Exists(stored))
            {
                var loaderIsManaged = installedState?.ManagedFiles.Any(file =>
                    Normalize(file).Equals(ActiveLoaderRelativePath, StringComparison.OrdinalIgnoreCase)) == true;
                if (!loaderIsManaged)
                    return new OperationResult(false,
                        "Existe um dwmapi.dll que não pertence ao modpack. Ele não foi alterado.");

                Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
                File.Move(active, stored);
                return new OperationResult(true, "Mods desativados. A Steam abrirá o jogo vanilla.");
            }

            if (!FilesMatch(active, stored))
                return new OperationResult(false,
                    "O dwmapi.dll ativo é diferente do carregador do modpack. Ele não foi removido.");

            File.Delete(active);
            return new OperationResult(true, "Mods desativados. A Steam abrirá o jogo vanilla.");
        }
        catch (Exception exception)
        {
            return new OperationResult(false, "Não foi possível ativar o modo vanilla:\n" + exception.Message);
        }
    }

    public OperationResult Activate(
        string gameRoot,
        IReadOnlyCollection<string>? enabledMods = null,
        InstalledState? installedState = null)
    {
        try
        {
            if (enabledMods is not null)
            {
                var selection = new ModSelectionService()
                    .ApplySelection(gameRoot, enabledMods, installedState);
                if (!selection.Success) return selection;
            }

            var win64 = ResolveWin64(gameRoot);
            var active = Path.Combine(win64, ActiveLoaderRelativePath);
            var stored = Path.Combine(win64, StoredLoaderRelativePath);
            if (!File.Exists(stored))
                return new OperationResult(false,
                    "O carregador dos mods não foi encontrado. Atualize o modpack antes de jogar.");

            if (File.Exists(active))
            {
                return FilesMatch(active, stored)
                    ? new OperationResult(true, "Mods já estão ativos.")
                    : new OperationResult(false,
                        "Já existe outro dwmapi.dll ativo. Ele não foi substituído.");
            }

            var temporary = active + ".modpack-temporario";
            File.Copy(stored, temporary, overwrite: true);
            File.Move(temporary, active);
            return new OperationResult(
                true,
                enabledMods is null
                    ? "Mods ativados para esta sessão."
                    : $"{enabledMods.Count} mod(s) ativado(s) para esta sessão.");
        }
        catch (Exception exception)
        {
            return new OperationResult(false, "Não foi possível ativar os mods:\n" + exception.Message);
        }
    }

    public bool IsActive(string gameRoot)
    {
        try
        {
            var win64 = ResolveWin64(gameRoot);
            return File.Exists(Path.Combine(win64, ActiveLoaderRelativePath));
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveWin64(string gameRoot)
    {
        var resolved = PalworldLocator.Resolve(gameRoot)
            ?? throw new DirectoryNotFoundException("A instalação do Palworld não foi encontrada.");
        return Path.Combine(resolved, Win64RelativePath);
    }

    private static bool FilesMatch(string first, string second)
    {
        var firstInfo = new FileInfo(first);
        var secondInfo = new FileInfo(second);
        if (firstInfo.Length != secondInfo.Length) return false;

        using var firstStream = File.OpenRead(first);
        using var secondStream = File.OpenRead(second);
        var firstHash = SHA256.HashData(firstStream);
        var secondHash = SHA256.HashData(secondStream);
        return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
