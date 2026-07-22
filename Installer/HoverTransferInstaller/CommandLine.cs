namespace HoverTransferInstaller;

internal static class CommandLine
{
    public static int Run(string[] args)
    {
        try
        {
            var command = args[0].ToLowerInvariant();
            if (command == "--verify")
            {
                using var payload = PayloadArchive.Open();
                PayloadArchive.Validate(payload);
                Console.WriteLine("PAYLOAD_OK");
                return 0;
            }

            if ((command == "--install" || command == "--uninstall") && args.Length >= 2)
            {
                var gameRoot = PalworldLocator.Resolve(args[1])
                    ?? throw new InvalidOperationException("Pasta do Palworld invalida.");
                var service = new InstallerService();
                var result = command == "--install"
                    ? service.Install(gameRoot)
                    : service.UninstallMod(gameRoot);
                Console.WriteLine(result.Message);
                return result.Success ? 0 : 2;
            }

            Console.Error.WriteLine("Uso: --verify | --install <pasta> | --uninstall <pasta>");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }
}
