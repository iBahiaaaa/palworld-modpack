using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PalworldModpackLauncher;

internal sealed class LauncherInstallationManager
{
    private const string ExecutableName = "Palworld-Modpack-Launcher.exe";
    private const string ShortcutName = "Palncher.lnk";
    private const string LegacyShortcutName = "Palworld Modpack Launcher.lnk";

    public string InstalledExecutablePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Bahiaaaa",
        "PalworldModpackLauncher",
        ExecutableName);

    public bool IsRunningInstalled =>
        PathsEqual(Environment.ProcessPath, InstalledExecutablePath);

    public bool IsInstalled => File.Exists(InstalledExecutablePath);

    public OperationResult Install()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "Bahiaaaa");
        var result = InstallTo(
            InstalledExecutablePath,
            Path.Combine(desktop, ShortcutName),
            Path.Combine(startMenu, ShortcutName));
        if (result.Success)
        {
            TryDelete(Path.Combine(desktop, LegacyShortcutName));
            TryDelete(Path.Combine(startMenu, LegacyShortcutName));
        }
        return result;
    }

    public OperationResult InstallForTest(string testRoot)
    {
        var root = Path.GetFullPath(testRoot);
        return InstallTo(
            Path.Combine(root, "AppData", ExecutableName),
            Path.Combine(root, "Desktop", ShortcutName),
            Path.Combine(root, "StartMenu", ShortcutName));
    }

    public OperationResult OpenInstalled()
    {
        try
        {
            if (!File.Exists(InstalledExecutablePath))
                return new OperationResult(false, "A instalação local do launcher não foi encontrada.");
            Process.Start(new ProcessStartInfo(InstalledExecutablePath) { UseShellExecute = true });
            return new OperationResult(true, "Palncher instalado iniciado.");
        }
        catch (Exception exception)
        {
            return new OperationResult(false, "Não foi possível abrir o launcher instalado:\n" + exception.Message);
        }
    }

    private static OperationResult InstallTo(
        string destination,
        string desktopShortcut,
        string startMenuShortcut)
    {
        try
        {
            var source = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                return new OperationResult(false, "Não foi possível localizar o executável atual do launcher.");

            destination = Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (!PathsEqual(source, destination))
            {
                var temporary = destination + ".novo";
                File.Copy(source, temporary, overwrite: true);
                File.Move(temporary, destination, overwrite: true);
            }

            CreateShortcut(desktopShortcut, destination);
            CreateShortcut(startMenuShortcut, destination);
            return new OperationResult(true,
                "Palncher instalado com sucesso.\n\n" +
                "Atalhos criados na Área de Trabalho e no Menu Iniciar.");
        }
        catch (Exception exception)
        {
            return new OperationResult(false, "Não foi possível instalar o launcher:\n" + exception.Message);
        }
    }

    private static void CreateShortcut(string shortcutPath, string executablePath)
    {
        shortcutPath = Path.GetFullPath(shortcutPath);
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("O serviço de atalhos do Windows não está disponível.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Não foi possível iniciar o serviço de atalhos.");
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath })
                ?? throw new InvalidOperationException("Não foi possível criar o atalho.");
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut,
                new object[] { executablePath });
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut,
                new object[] { Path.GetDirectoryName(executablePath)! });
            shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut,
                new object[] { executablePath + ",0" });
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut,
                new object[] { "Palncher: atualiza e inicia o Palworld com o modpack." });
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    private static bool PathsEqual(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
        return Path.GetFullPath(first).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    }

    public void RefreshBranding()
    {
        if (!IsInstalled) return;
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                "Bahiaaaa");
            CreateShortcut(Path.Combine(desktop, ShortcutName), InstalledExecutablePath);
            CreateShortcut(Path.Combine(startMenu, ShortcutName), InstalledExecutablePath);
            TryDelete(Path.Combine(desktop, LegacyShortcutName));
            TryDelete(Path.Combine(startMenu, LegacyShortcutName));
        }
        catch
        {
            // A identidade visual do atalho será atualizada na próxima instalação manual.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Um atalho em uso não deve impedir o funcionamento do Palncher.
        }
    }
}
