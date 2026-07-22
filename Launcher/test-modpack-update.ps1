param(
    [string]$PreviousVersion = "0.5.0",
    [string]$NewVersion = "0.6.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $projectRoot "Releases\Palworld-Modpack-Launcher.exe"
$previousRoot = Join-Path $PSScriptRoot "test-output\fixtures\v$PreviousVersion"
$previousPackage = Join-Path $previousRoot "palworld-modpack.zip"
$previousChecksum = ((Get-Content -LiteralPath "$previousPackage.sha256" -Raw).Trim() -split '\s+')[0]
$newPackage = Join-Path $projectRoot "Releases\palworld-modpack.zip"
$newChecksum = ((Get-Content -LiteralPath "$newPackage.sha256" -Raw).Trim() -split '\s+')[0]
$testRoot = Join-Path $PSScriptRoot "test-output\update-cycle"
$gameRoot = Join-Path $testRoot "SteamLibrary\steamapps\common\Palworld"
$win64 = Join-Path $gameRoot "Pal\Binaries\Win64"

if (Test-Path -LiteralPath $testRoot) {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "test-output")) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Pasta de teste insegura: $resolved"
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $win64 | Out-Null
Set-Content -LiteralPath (Join-Path $win64 "Palworld-Win64-Shipping.exe") -Value "arquivo de teste" -Encoding ascii

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

Invoke-Launcher @("--apply-local", $gameRoot, $previousPackage, $PreviousVersion, "v$PreviousVersion", $previousChecksum)
$altTabMarker = Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\enabled.txt"
$altTabMain = Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\Scripts\main.lua"
$previousVersionValue = [Version]$PreviousVersion
$previousAltTabHash = $null
if ($previousVersionValue -lt [Version]'0.4.0') {
    if (Test-Path -LiteralPath $altTabMarker) { throw "A fixture anterior à v0.4.0 já contém o AltTab." }
} else {
    if (-not (Test-Path -LiteralPath $altTabMain)) { throw "A fixture v$PreviousVersion não contém o AltTab esperado." }
    $previousAltTabHash = (Get-FileHash -LiteralPath $altTabMain -Algorithm SHA256).Hash
}
$hoverMarker = Join-Path $win64 "ue4ss\Mods\HoverTransfer\enabled.txt"
$accessoryMarker = Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\enabled.txt"
if ($previousVersionValue -lt [Version]'0.5.0' -and (Test-Path -LiteralPath $accessoryMarker)) {
    throw "A fixture anterior à v0.5.0 já contém Accessory Slots Research."
}
if (-not (Test-Path -LiteralPath $hoverMarker)) { throw "Hover Transfer ausente na versão anterior." }
$externalFile = Join-Path $win64 "ue4ss\Mods\OutroMod\arquivo-preservado.txt"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $externalFile) | Out-Null
Set-Content -LiteralPath $externalFile -Value "preservar" -Encoding utf8

Invoke-Launcher @("--apply-local", $gameRoot, $newPackage, $NewVersion, "v$NewVersion", $newChecksum)

$required = @(
    $hoverMarker,
    $altTabMarker,
    $altTabMain,
    (Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\Scripts\continuation_policy.lua"),
    (Join-Path $win64 "ue4ss\Mods\AltTabWorkContinuation\Scripts\AltTabWorkContinuationFocus.dll"),
    $accessoryMarker,
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\main.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\config.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\slot_limits.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\equipment_storage.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\slot_expander.lua"),
    (Join-Path $win64 "ue4ss\Mods\AccessorySlotsResearch\Scripts\slot_refresher.lua"),
    (Join-Path $win64 "Palworld-Modpack\loader\dwmapi.dll"),
    $externalFile
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Arquivo esperado ausente: $path" }
}
if ($previousAltTabHash) {
    if (-not (Test-Path -LiteralPath $altTabMain)) { throw "O AltTab existente foi removido durante a atualização." }
}

$statePath = Join-Path $win64 "ue4ss\palworld-modpack-state.json"
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($state.version -ne $NewVersion) { throw "Versão final incorreta: $($state.version)" }
if ($state.managedFiles.Count -lt 26) { throw "Arquivos gerenciados incompletos: $($state.managedFiles.Count)" }
if (Test-Path -LiteralPath (Join-Path $win64 "dwmapi.dll")) { throw "A atualização deixou os mods ativos para a Steam." }

Write-Host "Atualização simulada v$PreviousVersion -> v$NewVersion concluída."
