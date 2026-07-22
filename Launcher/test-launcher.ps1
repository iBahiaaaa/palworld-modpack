param(
    [string]$ModpackVersion = "0.3.0",
    [string]$LauncherVersion = "1.0.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $projectRoot "Releases\Palworld-Modpack-Launcher.exe"
$package = Join-Path $projectRoot "Releases\palworld-modpack.zip"
$checksum = ((Get-Content -LiteralPath "$package.sha256" -Raw).Trim() -split '\s+')[0]
$testRoot = Join-Path $PSScriptRoot "test-output"
$gameRoot = Join-Path $testRoot "SteamLibrary\steamapps\common\Palworld"
$win64 = Join-Path $gameRoot "Pal\Binaries\Win64"

if (Test-Path -LiteralPath $testRoot) {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath($PSScriptRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Pasta de teste insegura: $resolved"
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $win64 | Out-Null
Set-Content -LiteralPath (Join-Path $win64 "Palworld-Win64-Shipping.exe") -Value "arquivo de teste" -Encoding ascii
Set-Content -LiteralPath (Join-Path $win64 "arquivo-preservado.txt") -Value "preservar" -Encoding utf8

function Invoke-Launcher([string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $launcher
    $info.Arguments = ($Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Launcher falhou ($($process.ExitCode)): $stderr $stdout" }
    Write-Host $stdout.Trim()
}

Invoke-Launcher @("--apply-local", (Join-Path $testRoot "SteamLibrary"), $package, $ModpackVersion, "v$ModpackVersion", $checksum)
Invoke-Launcher @("--status", $gameRoot)

$required = @(
    (Join-Path $win64 "dwmapi.dll"),
    (Join-Path $win64 "ue4ss\UE4SS.dll"),
    (Join-Path $win64 "ue4ss\Mods\HoverTransfer\enabled.txt"),
    (Join-Path $win64 "ue4ss\palworld-modpack-state.json"),
    (Join-Path $win64 "arquivo-preservado.txt")
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Arquivo esperado não encontrado: $path" }
}

$state = Get-Content -LiteralPath (Join-Path $win64 "ue4ss\palworld-modpack-state.json") -Raw | ConvertFrom-Json
if ($state.version -ne $ModpackVersion) { throw "Versão instalada incorreta: $($state.version)" }
if ($state.managedFiles.Count -lt 5) { throw "Lista de arquivos gerenciados incompleta." }

$protectedHash = (Get-FileHash -LiteralPath (Join-Path $win64 "ue4ss\UE4SS.dll") -Algorithm SHA256).Hash
$info = [Diagnostics.ProcessStartInfo]::new()
$info.FileName = $launcher
$info.Arguments = '"--apply-local" "' + $gameRoot + '" "' + $package + '" "' + $ModpackVersion + '" "v' + $ModpackVersion + '" "0000"'
$info.UseShellExecute = $false
$process = [Diagnostics.Process]::Start($info)
$process.WaitForExit()
if ($process.ExitCode -eq 0) { throw "Pacote com hash inválido foi aceito." }
$currentHash = (Get-FileHash -LiteralPath (Join-Path $win64 "ue4ss\UE4SS.dll") -Algorithm SHA256).Hash
if ($currentHash -ne $protectedHash) { throw "O teste de hash alterou a instalação válida." }

Write-Host "Teste completo do launcher concluído."
