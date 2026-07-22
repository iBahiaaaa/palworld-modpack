param(
    [string]$LauncherVersion = "1.1.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $projectRoot "Releases\Palworld-Modpack-Launcher.exe"
$manifest = Join-Path $projectRoot "Releases\launcher-version.json"
$testOutputRoot = Join-Path $PSScriptRoot "test-output"
$testRoot = Join-Path $testOutputRoot "self-update"

if (Test-Path -LiteralPath $testRoot) {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath($testOutputRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Pasta de teste insegura: $resolved"
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null

$metadata = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($metadata.version -ne $LauncherVersion) {
    throw "Versão incorreta no manifesto: $($metadata.version)"
}

$destination = Join-Path $testRoot "Palworld-Modpack-Launcher.exe"
Set-Content -LiteralPath $destination -Value "launcher antigo de teste" -Encoding ascii
$info = [Diagnostics.ProcessStartInfo]::new()
$info.FileName = $launcher
$info.Arguments = '"--replace-launcher" "0" "' + $destination.Replace('"', '\"') + '" "' + $testRoot.Replace('"', '\"') + '" "--no-restart"'
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$process = [Diagnostics.Process]::Start($info)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Substituição do launcher falhou: $($process.ExitCode)" }

$sourceHash = (Get-FileHash -LiteralPath $launcher -Algorithm SHA256).Hash
$destinationHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
if ($sourceHash -ne $destinationHash) { throw "O executável atualizado não corresponde ao original." }

Write-Host "Teste de autoatualização do launcher concluído."
