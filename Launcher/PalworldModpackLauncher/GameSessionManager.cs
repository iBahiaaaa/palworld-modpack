using System.Diagnostics;

namespace PalworldModpackLauncher;

internal sealed class GameSessionManager
{
    private readonly ModActivationManager activationManager = new();

    public bool IsSessionActive { get; private set; }

    public async Task<OperationResult> LaunchModdedAsync(
        string gameRoot,
        Action? gameStarted = null,
        CancellationToken cancellationToken = default)
    {
        if (IsPalworldRunning())
            return new OperationResult(false, "O Palworld já está aberto. Feche-o antes de iniciar com mods.");

        var activated = activationManager.Activate(gameRoot);
        if (!activated.Success) return activated;

        IsSessionActive = true;
        OperationResult sessionResult;
        try
        {
            LaunchThroughSteam();
            var started = await WaitForGameStartAsync(TimeSpan.FromMinutes(2), cancellationToken);
            if (!started)
                sessionResult = new OperationResult(false, "O Palworld não iniciou dentro do tempo esperado.");
            else
            {
                gameStarted?.Invoke();
                await WaitForGameExitAsync(cancellationToken);
                sessionResult = new OperationResult(true, "Palworld encerrado. Os mods foram desativados.");
            }

        }
        catch (TaskCanceledException)
        {
            sessionResult = new OperationResult(false, "O acompanhamento da sessão foi cancelado.");
        }
        catch (Exception exception)
        {
            sessionResult = new OperationResult(false, "Não foi possível iniciar o Palworld:\n" + exception.Message);
        }

        var vanilla = activationManager.EnsureVanilla(gameRoot);
        IsSessionActive = false;
        return vanilla.Success ? sessionResult : vanilla;
    }

    public OperationResult LaunchVanilla(string gameRoot, InstalledState? state)
    {
        if (IsPalworldRunning())
            return new OperationResult(false, "O Palworld já está aberto.");

        var vanilla = activationManager.EnsureVanilla(gameRoot, state);
        if (!vanilla.Success) return vanilla;

        try
        {
            LaunchThroughSteam();
            return new OperationResult(true, "Palworld iniciado em modo vanilla.");
        }
        catch (Exception exception)
        {
            return new OperationResult(false, "Não foi possível iniciar o Palworld:\n" + exception.Message);
        }
    }

    public OperationResult EnsureVanilla(string gameRoot, InstalledState? state = null) =>
        activationManager.EnsureVanilla(gameRoot, state);

    public static bool IsPalworldRunning() => CountRunningProcesses() > 0;

    public static bool IsPalworldRunning(string gameRoot)
    {
        var resolvedRoot = PalworldLocator.Resolve(gameRoot);
        if (resolvedRoot is null) return IsPalworldRunning();
        var rootPrefix = Path.GetFullPath(resolvedRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var processes = GetRunningProcesses();
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var executable = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(executable) &&
                        Path.GetFullPath(executable).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // Se não for possível consultar o caminho, mantém o bloqueio por segurança.
                    return true;
                }
            }
            return false;
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static void LaunchThroughSteam()
    {
        Process.Start(new ProcessStartInfo("steam://rungameid/1623730") { UseShellExecute = true });
    }

    private static async Task<bool> WaitForGameStartAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountRunningProcesses() > 0) return true;
            await Task.Delay(1000, cancellationToken);
        }
        return false;
    }

    private static async Task WaitForGameExitAsync(CancellationToken cancellationToken)
    {
        var emptyChecks = 0;
        while (emptyChecks < 3)
        {
            emptyChecks = CountRunningProcesses() == 0 ? emptyChecks + 1 : 0;
            await Task.Delay(1000, cancellationToken);
        }
    }

    private static int CountRunningProcesses()
    {
        var processes = GetRunningProcesses();
        foreach (var process in processes) process.Dispose();
        return processes.Length;
    }

    private static Process[] GetRunningProcesses() =>
        Process.GetProcessesByName("Palworld-Win64-Shipping")
            .Concat(Process.GetProcessesByName("Palworld"))
            .ToArray();
}
