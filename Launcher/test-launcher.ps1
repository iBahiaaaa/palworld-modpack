param(
    [string]$ModpackVersion = "0.7.0",
    [string]$LauncherVersion = "1.7.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $projectRoot "Releases\Palworld-Modpack-Launcher.exe"
$package = Join-Path $projectRoot "Releases\palworld-modpack.zip"
$checksum = ((Get-Content -LiteralPath "$package.sha256" -Raw).Trim() -split '\s+')[0]
$testOutputRoot = Join-Path $PSScriptRoot "test-output"
$testRoot = Join-Path $testOutputRoot "clean-install"
$gameRoot = Join-Path $testRoot "SteamLibrary\steamapps\common\Palworld"
$win64 = Join-Path $gameRoot "Pal\Binaries\Win64"

if (Test-Path -LiteralPath $testRoot) {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath($testOutputRoot) + [IO.Path]::DirectorySeparatorChar
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
    (Join-Path $win64 "Palworld-Modpack\loader\dwmapi.dll"),
    (Join-Path $win64 "ue4ss\UE4SS.dll"),
    (Join-Path $win64 "ue4ss\Mods\HoverTransfer\enabled.txt"),
    (Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\enabled.txt"),
    (Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\Scripts\AltTabWorkContinuationFocus.dll"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\enabled.txt"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\main.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\slot_limits.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\equipment_storage.lua"),
    (Join-Path $win64 "ue4ss\Mods\ItemStackExtender\enabled.txt"),
    (Join-Path $win64 "ue4ss\Mods\ItemStackExtender\config.json"),
    (Join-Path $win64 "ue4ss\Mods\ItemStackExtender\Scripts\main.lua"),
    (Join-Path $win64 "ue4ss\Mods\ItemStackExtender\Scripts\static_data_sync.lua"),
    (Join-Path $win64 "ue4ss\palworld-modpack-state.json"),
    (Join-Path $win64 "arquivo-preservado.txt")
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Arquivo esperado não encontrado: $path" }
}
if (Test-Path -LiteralPath (Join-Path $win64 "dwmapi.dll")) { throw "O carregador ficou ativo após a instalação." }

$state = Get-Content -LiteralPath (Join-Path $win64 "ue4ss\palworld-modpack-state.json") -Raw | ConvertFrom-Json
if ($state.version -ne $ModpackVersion) { throw "Versão instalada incorreta: $($state.version)" }
if ($state.managedFiles.Count -lt 34) { throw "Lista de arquivos gerenciados incompleta." }

Invoke-Launcher @("--set-enabled-mods", $gameRoot, "HoverTransfer")
if (-not (Test-Path -LiteralPath (Join-Path $win64 "ue4ss\Mods\HoverTransfer\enabled.txt"))) {
    throw "O mod selecionado foi desativado."
}
foreach ($disabledMarker in @(
    (Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\enabled.txt"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\enabled.txt"),
    (Join-Path $win64 "ue4ss\Mods\ItemStackExtender\enabled.txt")
)) {
    if (Test-Path -LiteralPath $disabledMarker) {
        throw "Um mod desmarcado permaneceu ativo: $disabledMarker"
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\main.lua"))) {
    throw "Desativar um mod removeu os arquivos dele."
}

Invoke-Launcher @("--activate-mods", $gameRoot)
if (-not (Test-Path -LiteralPath (Join-Path $win64 "dwmapi.dll"))) { throw "Os mods não foram ativados." }
Invoke-Launcher @("--disable-mods", $gameRoot)
if (Test-Path -LiteralPath (Join-Path $win64 "dwmapi.dll")) { throw "Os mods não voltaram ao modo vanilla." }

$conflictingLoader = Join-Path $win64 "dwmapi.dll"
Set-Content -LiteralPath $conflictingLoader -Value "carregador externo de teste" -Encoding ascii
$conflictInfo = [Diagnostics.ProcessStartInfo]::new()
$conflictInfo.FileName = $launcher
$conflictInfo.Arguments = '"--disable-mods" "' + $gameRoot.Replace('"', '\"') + '"'
$conflictInfo.UseShellExecute = $false
$conflictProcess = [Diagnostics.Process]::Start($conflictInfo)
$conflictProcess.WaitForExit()
if ($conflictProcess.ExitCode -eq 0) { throw "Um carregador externo foi removido sem bloqueio." }
if (-not (Test-Path -LiteralPath $conflictingLoader)) { throw "O carregador externo não foi preservado." }
Remove-Item -LiteralPath $conflictingLoader -Force

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

$externalFile = Join-Path $win64 "ue4ss\Mods\OutroMod\arquivo-preservado.txt"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $externalFile) | Out-Null
Set-Content -LiteralPath $externalFile -Value "preservar" -Encoding utf8
Invoke-Launcher @("--uninstall-modpack", $gameRoot)
$removedPaths = @(
    (Join-Path $win64 "dwmapi.dll"),
    (Join-Path $win64 "Palworld-Modpack"),
    (Join-Path $win64 "ue4ss\UE4SS.dll"),
    (Join-Path $win64 "ue4ss\palworld-modpack-state.json"),
    (Join-Path $win64 "ue4ss\Mods\HoverTransfer"),
    (Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch"),
    (Join-Path $win64 "ue4ss\Mods\ItemStackExtender")
)
foreach ($path in $removedPaths) {
    if (Test-Path -LiteralPath $path) { throw "A remoção preservou um arquivo do modpack: $path" }
}
if (-not (Test-Path -LiteralPath $externalFile)) { throw "A remoção apagou um mod externo." }
$uninstallBackups = @(Get-ChildItem -LiteralPath (Join-Path $win64 "Palworld-Modpack-Backups") -Directory -Filter "launcher-removal-*")
if ($uninstallBackups.Count -lt 1) { throw "O backup da remoção não foi criado." }

Invoke-Launcher @("--apply-local", $gameRoot, $package, $ModpackVersion, "v$ModpackVersion", $checksum)
if (-not (Test-Path -LiteralPath (Join-Path $win64 "ue4ss\palworld-modpack-state.json"))) { throw "A reinstalação não recriou o estado." }
if (Test-Path -LiteralPath (Join-Path $win64 "dwmapi.dll")) { throw "A reinstalação deixou a Steam com mods ativos." }

Write-Host "Teste completo do launcher concluído."
