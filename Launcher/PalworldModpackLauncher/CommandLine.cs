namespace PalworldModpackLauncher;

internal static class CommandLine
{
    public static int Run(string[] args)
    {
        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "--apply-local" => ApplyLocal(args),
                "--status" => Status(args),
                "--check-release" => CheckRelease().GetAwaiter().GetResult(),
                _ => Fail("Comando desconhecido."),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int ApplyLocal(string[] args)
    {
        if (args.Length != 6)
            return Fail("Uso: --apply-local <pasta> <zip> <versão> <tag> <sha256>");
        if (!Version.TryParse(args[3], out var version)) return Fail("Versão inválida.");
        var result = new ModpackInstaller().ApplyPackage(args[1], args[2], args[5], version, args[4]);
        Console.WriteLine(result.Message);
        return result.Success ? 0 : 1;
    }

    private static int Status(string[] args)
    {
        if (args.Length != 2) return Fail("Uso: --status <pasta>");
        var state = new ModpackInstaller().ReadState(args[1]);
        if (state is null) return Fail("Modpack não instalado.");
        Console.WriteLine($"{state.Version}|{state.ReleaseTag}|{state.ManagedFiles.Count}");
        return 0;
    }

    private static async Task<int> CheckRelease()
    {
        using var client = new GitHubReleaseClient();
        var update = await client.GetLatestAsync();
        Console.WriteLine($"{update.Version.ToString(3)}|{update.Package.Name}|{update.Package.Size}");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
