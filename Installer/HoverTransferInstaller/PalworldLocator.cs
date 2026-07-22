using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace HoverTransferInstaller;

internal static class PalworldLocator
{
    private static readonly string ExecutableRelativePath =
        Path.Combine("Pal", "Binaries", "Win64", "Palworld-Win64-Shipping.exe");

    public static string? Resolve(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath)) return null;

        string selected;
        try { selected = Path.GetFullPath(selectedPath.Trim().Trim('"')); }
        catch { return null; }

        if (File.Exists(selected)) selected = Path.GetDirectoryName(selected)!;

        var candidates = new List<string>
        {
            selected,
            Path.Combine(selected, "Palworld"),
            Path.Combine(selected, "common", "Palworld"),
            Path.Combine(selected, "steamapps", "common", "Palworld"),
        };

        var current = new DirectoryInfo(selected);
        for (var depth = 0; current is not null && depth < 7; depth++, current = current.Parent)
        {
            candidates.Add(current.FullName);
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(candidate, ExecutableRelativePath)))
            {
                return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    public static string? DetectInstalledGame()
    {
        foreach (var library in EnumerateSteamLibraries())
        {
            var resolved = Resolve(library);
            if (resolved is not null) return resolved;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateSteamLibraries()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRegistryPath(roots, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        AddRegistryPath(roots, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        AddRegistryPath(roots, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));

        foreach (var root in roots.ToArray())
        {
            if (!Directory.Exists(root)) continue;
            yield return root;

            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile)) continue;

            string text;
            try { text = File.ReadAllText(libraryFile); }
            catch { continue; }

            foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\""))
            {
                var path = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path)) yield return path;
            }
        }
    }

    private static void AddRegistryPath(HashSet<string> paths, RegistryKey hive, string keyPath, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath);
            if (key?.GetValue(valueName) is string value && Directory.Exists(value)) paths.Add(value);
        }
        catch
        {
            // A detecção continua pelas outras fontes.
        }
    }
}
