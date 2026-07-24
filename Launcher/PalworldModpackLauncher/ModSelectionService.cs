namespace PalworldModpackLauncher;

internal sealed class ModSelectionService
{
    private static readonly string Win64RelativePath = Path.Combine("Pal", "Binaries", "Win64");
    private static readonly HashSet<string> CoreMods = new(StringComparer.OrdinalIgnoreCase)
    {
        "CheatManagerEnablerMod",
        "ConsoleCommandsMod",
        "ConsoleEnablerMod",
        "SplitScreenMod",
        "LineTraceMod",
        "BPML_GenericFunctions",
        "BPModLoaderMod",
        "Keybinds",
    };

    private static readonly Dictionary<string, ModDetails> KnownMods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["HoverTransfer"] = new(
                "Distribuidor rápido de itens",
                "Segure H sobre um item para enviá-lo ao baú aberto.",
                10,
                true),
            ["AltTabWorkContinuation"] = new(
                "Fabricação durante Alt+Tab",
                "Mantém a interação de fabricação quando o jogo perde o foco.",
                20,
                true),
            ["AccessorySlotsResearch"] = new(
                "Slots extras de acessórios",
                "Aumenta e salva os espaços disponíveis para acessórios.",
                30,
                true),
            ["ItemStackExtender"] = new(
                "Pilhas de itens até 100.000",
                "Aumenta para 100.000 o limite dos materiais empilháveis.",
                40,
                true),
            ["PartySlotsResearch"] = new(
                "Slots extras de Pals (experimental)",
                "Em desenvolvimento. Permanece desativado por padrão.",
                50,
                false),
        };

    public IReadOnlyList<ModOption> Discover(string gameRoot, InstalledState? installedState)
    {
        var win64 = ResolveWin64(gameRoot);
        var modsRoot = Path.Combine(win64, "ue4ss", "Mods");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var managed in installedState?.ManagedFiles ?? Array.Empty<string>())
        {
            var normalized = managed.Replace('\\', '/').TrimStart('/');
            const string prefix = "ue4ss/Mods/";
            const string suffix = "/enabled.txt";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var name = normalized[prefix.Length..^suffix.Length];
            if (IsSafeModName(name) && !CoreMods.Contains(name)) names.Add(name);
        }

        if (Directory.Exists(modsRoot))
        {
            foreach (var directory in Directory.EnumerateDirectories(modsRoot))
            {
                var name = Path.GetFileName(directory);
                if (!IsSafeModName(name) || CoreMods.Contains(name)) continue;
                var marker = Path.Combine(directory, "enabled.txt");
                var mainScript = Path.Combine(directory, "Scripts", "main.lua");
                if (File.Exists(marker) || File.Exists(mainScript)) names.Add(name);
            }
        }

        return names
            .Select(CreateOption)
            .OrderBy(option => KnownMods.TryGetValue(option.Id, out var details) ? details.Order : 1000)
            .ThenBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public IReadOnlyCollection<string> ResolveSelection(
        string gameRoot,
        IReadOnlyList<ModOption> options,
        LauncherSettings settings)
    {
        if (PathsEqual(settings.GameRoot, gameRoot) && settings.EnabledMods is not null)
        {
            var available = options.Select(option => option.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return settings.EnabledMods
                .Where(available.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return options
            .Where(option => option.EnabledByDefault)
            .Select(option => option.Id)
            .ToArray();
    }

    public OperationResult ApplySelection(
        string gameRoot,
        IReadOnlyCollection<string> enabledMods,
        InstalledState? installedState)
    {
        try
        {
            var win64 = ResolveWin64(gameRoot);
            var selected = enabledMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var options = Discover(gameRoot, installedState);
            var available = options.Select(option => option.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknown = selected.Where(name => !available.Contains(name)).ToArray();
            if (unknown.Length > 0)
                return new OperationResult(
                    false,
                    "A seleção contém mods que não estão instalados: " + string.Join(", ", unknown));

            foreach (var option in options)
            {
                var modDirectory = SafeModDirectory(win64, option.Id);
                var marker = Path.Combine(modDirectory, "enabled.txt");
                if (selected.Contains(option.Id))
                {
                    if (!Directory.Exists(modDirectory))
                        return new OperationResult(false, $"A pasta do mod {option.DisplayName} não foi encontrada.");
                    if (!File.Exists(marker)) File.WriteAllText(marker, string.Empty);
                }
                else if (File.Exists(marker))
                {
                    File.Delete(marker);
                }
            }

            return new OperationResult(
                true,
                $"{selected.Count} mod(s) selecionado(s) para esta sessão.");
        }
        catch (Exception exception)
        {
            return new OperationResult(
                false,
                "Não foi possível aplicar a seleção de mods:\n" + exception.Message);
        }
    }

    private static ModOption CreateOption(string id)
    {
        if (KnownMods.TryGetValue(id, out var details))
            return new ModOption(id, details.DisplayName, details.Description, details.EnabledByDefault);

        return new ModOption(
            id,
            SplitName(id),
            "Mod adicional encontrado nesta instalação.",
            true);
    }

    private static string SplitName(string value)
    {
        var result = new System.Text.StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            if (index > 0 && char.IsUpper(value[index]) && !char.IsUpper(value[index - 1]))
                result.Append(' ');
            result.Append(value[index]);
        }
        return result.ToString();
    }

    private static string ResolveWin64(string gameRoot)
    {
        var resolved = PalworldLocator.Resolve(gameRoot)
            ?? throw new DirectoryNotFoundException("A instalação do Palworld não foi encontrada.");
        return Path.GetFullPath(Path.Combine(resolved, Win64RelativePath));
    }

    private static string SafeModDirectory(string win64, string modName)
    {
        if (!IsSafeModName(modName))
            throw new InvalidDataException("Nome de mod inválido: " + modName);
        var modsRoot = Path.GetFullPath(Path.Combine(win64, "ue4ss", "Mods"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(modsRoot, modName));
        if (!combined.StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Caminho de mod inseguro.");
        return combined;
    }

    private static bool IsSafeModName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.IndexOfAny(new[]
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            ':',
        }) < 0 &&
        value is not "." and not "..";

    private static bool PathsEqual(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
        try
        {
            return Path.GetFullPath(first).Equals(
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private sealed record ModDetails(
        string DisplayName,
        string Description,
        int Order,
        bool EnabledByDefault);
}
