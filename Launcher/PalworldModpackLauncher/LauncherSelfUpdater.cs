using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace PalworldModpackLauncher;

internal sealed class LauncherSelfUpdater : IDisposable
{
    private readonly GitHubReleaseClient releaseClient = new();

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public async Task<LauncherUpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var latest = await releaseClient.GetLatestLauncherAsync(cancellationToken);
        return latest is not null && latest.Version > CurrentVersion ? latest : null;
    }

    public async Task<OperationResult> DownloadAndRestartAsync(
        LauncherUpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable) || !File.Exists(currentExecutable))
            return new OperationResult(false, "Não foi possível localizar o executável atual do launcher.");

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "PalworldModpackLauncher",
            "self-update",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var downloaded = Path.Combine(tempDirectory, "Palworld-Modpack-Launcher-atualizacao.exe");
            var checksumText = await releaseClient.DownloadTextAsync(update.Checksum, cancellationToken);
            await releaseClient.DownloadFileAsync(update.Executable, downloaded, progress, cancellationToken);
            ValidateHash(downloaded, checksumText);

            var startInfo = new ProcessStartInfo(downloaded)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--replace-launcher");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add(currentExecutable);
            startInfo.ArgumentList.Add(tempDirectory);
            Process.Start(startInfo);

            return new OperationResult(true,
                $"Palncher {update.Version.ToString(3)} baixado. Reiniciando para concluir a atualização.");
        }
        catch (Exception exception)
        {
            TryDeleteDirectory(tempDirectory);
            return new OperationResult(false, "Não foi possível atualizar o launcher:\n" + exception.Message);
        }
    }

    public static int ReplaceAndRestart(string[] args)
    {
        if (args.Length is < 4 or > 5 || !int.TryParse(args[1], out var previousProcessId))
            return Fail("Uso: --replace-launcher <pid> <destino> <pasta-temporária> [--no-restart]");

        var destination = Path.GetFullPath(args[2]);
        var tempDirectory = Path.GetFullPath(args[3]);
        var noRestart = args.Length == 5 && args[4].Equals("--no-restart", StringComparison.OrdinalIgnoreCase);
        var source = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            return Fail("Executável temporário não encontrado.");
        if (!destination.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return Fail("O destino do launcher é inválido.");

        WaitForProcess(previousProcessId, TimeSpan.FromSeconds(30));
        CopyWithRetry(source, destination, TimeSpan.FromSeconds(20));

        if (!noRestart)
        {
            var startInfo = new ProcessStartInfo(destination)
            {
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add("--cleanup-update");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add(tempDirectory);
            Process.Start(startInfo);
        }
        return 0;
    }

    public static void CleanupAfterReplacement(string[] args)
    {
        if (args.Length != 3 || !int.TryParse(args[1], out var helperProcessId)) return;
        WaitForProcess(helperProcessId, TimeSpan.FromSeconds(20));
        TryDeleteDirectory(args[2]);
    }

    private static void ValidateHash(string path, string expected)
    {
        var normalized = expected.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) normalized = normalized[7..];
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A verificação SHA-256 do launcher falhou.");
    }

    private static void WaitForProcess(int processId, TimeSpan timeout)
    {
        if (processId <= 0 || processId == Environment.ProcessId) return;
        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // O processo já encerrou.
        }
    }

    private static void CopyWithRetry(string source, string destination, TimeSpan timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        do
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException exception)
            {
                lastError = exception;
                Thread.Sleep(300);
            }
            catch (UnauthorizedAccessException exception)
            {
                lastError = exception;
                Thread.Sleep(300);
            }
        } while (DateTime.UtcNow < deadline);

        throw new IOException("Não foi possível substituir o launcher antigo.", lastError);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // A pasta temporária será limpa em uma próxima atualização.
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    public void Dispose() => releaseClient.Dispose();
}
