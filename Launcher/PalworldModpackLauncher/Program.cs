namespace PalworldModpackLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--cleanup-update", StringComparison.OrdinalIgnoreCase))
        {
            LauncherSelfUpdater.CleanupAfterReplacement(args);
            args = Array.Empty<string>();
        }

        if (args.Length > 0)
        {
            return CommandLine.Run(args);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
