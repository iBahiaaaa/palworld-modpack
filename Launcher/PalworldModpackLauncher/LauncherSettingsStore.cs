using System.Text.Json;

namespace PalworldModpackLauncher;

internal static class LauncherSettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PalworldModpackLauncher");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static LauncherSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(FilePath)) ?? new(null)
                : new(null);
        }
        catch
        {
            return new(null);
        }
    }

    public static void Save(string gameRoot, IReadOnlyCollection<string>? enabledMods = null)
    {
        Directory.CreateDirectory(DirectoryPath);
        var current = Load();
        var selection = enabledMods?.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
            ?? (PathsEqual(current.GameRoot, gameRoot) ? current.EnabledMods : null);
        File.WriteAllText(
            FilePath,
            JsonSerializer.Serialize(new LauncherSettings(gameRoot, selection), JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
}
