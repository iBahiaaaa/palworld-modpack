using System.Diagnostics;

namespace PalworldModpackLauncher;

internal sealed class GameSessionManager
{
    private readonly ModActivationManager activationManager = new();
    private readonly SemaphoreSlim restartGate = new(1, 1);
    private volatile bool restartInProgress;

    public bool IsSessionActive { get; private set; }
    public bool IsRestarting => restartInProgress;

    public async Task<OperationResult> LaunchModdedAsync(
        string gameRoot,
        IReadOnlyCollection<string> enabledMods,
        InstalledState? installedState,
        Action? gameStarted = null,
        CancellationToken cancellationToken = default)
    {
        if (IsPalworldRunning())
            return new OperationResult(false, "O Palworld já está aberto. Feche-o antes de iniciar com mods.");

        var activated = activationManager.Activate(gameRoot, enabledMods, installedState);
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

    public async Task<OperationResult> RestartAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsSessionActive)
            return new OperationResult(
                false,
                "Inicie o Palworld com mods pelo launcher antes de usar o reinício.");
        if (!IsPalworldRunning())
            return new OperationResult(false, "O Palworld não está aberto.");
        if (!await restartGate.WaitAsync(0, cancellationToken))
            return new OperationResult(false, "O Palworld já está sendo reiniciado.");

        restartInProgress = true;
        try
        {
            var stopped = await StopPalworldAsync(cancellationToken);
            if (!stopped)
                return new OperationResult(
                    false,
                    "Não foi possível encerrar completamente o Palworld.");

            await Task.Delay(1500, cancellationToken);
            LaunchThroughSteam();
            var started = await WaitForGameStartAsync(
                TimeSpan.FromMinutes(2),
                cancellationToken);
            return started
                ? new OperationResult(
                    true,
                    "Palworld reiniciado com os mesmos mods desta sessão.")
                : new OperationResult(
                    false,
                    "O Palworld foi fechado, mas não reabriu dentro do tempo esperado.");
        }
        catch (TaskCanceledException)
        {
            return new OperationResult(false, "O reinício do Palworld foi cancelado.");
        }
        catch (Exception exception)
        {
            return new OperationResult(
                false,
                "Não foi possível reiniciar o Palworld:\n" + exception.Message);
        }
        finally
        {
            restartInProgress = false;
            restartGate.Release();
        }
    }

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

    private async Task WaitForGameExitAsync(CancellationToken cancellationToken)
    {
        var emptyChecks = 0;
        while (emptyChecks < 3)
        {
            if (restartInProgress)
            {
                emptyChecks = 0;
                await Task.Delay(250, cancellationToken);
                continue;
            }
            emptyChecks = CountRunningProcesses() == 0 ? emptyChecks + 1 : 0;
            await Task.Delay(1000, cancellationToken);
        }
    }

    private static async Task<bool> StopPalworldAsync(
        CancellationToken cancellationToken)
    {
        var processes = GetRunningProcesses();
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited) process.CloseMainWindow();
                }
                catch
                {
                    // A finalização forçada abaixo cobre processos sem janela.
                }
            }
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }

        if (await WaitForGameStopAsync(TimeSpan.FromSeconds(8), cancellationToken))
            return true;

        processes = GetRunningProcesses();
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // A verificação final informa se algum processo resistiu.
                }
            }
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }

        return await WaitForGameStopAsync(
            TimeSpan.FromSeconds(20),
            cancellationToken);
    }

    private static async Task<bool> WaitForGameStopAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountRunningProcesses() == 0) return true;
            await Task.Delay(250, cancellationToken);
        }
        return CountRunningProcesses() == 0;
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
