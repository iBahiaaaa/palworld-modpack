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

    public static void Save(string gameRoot)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new LauncherSettings(gameRoot), JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
