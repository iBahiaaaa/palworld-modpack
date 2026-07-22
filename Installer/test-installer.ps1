param()

$ErrorActionPreference = 'Stop'
$installerRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $installerRoot
$installer = Join-Path $projectRoot 'Releases\HoverTransfer-Instalador-v0.3.0.exe'
$testRoot = Join-Path $installerRoot 'test-output'
$fakeGame = Join-Path $testRoot 'SteamLibrary\steamapps\common\Palworld'
$win64 = Join-Path $fakeGame 'Pal\Binaries\Win64'

function Invoke-Installer([string[]]$Arguments) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new($installer)
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = ($Arguments | ForEach-Object {
        '"' + $_.Replace('"', '\"') + '"'
    }) -join ' '
    $process = [Diagnostics.Process]::Start($startInfo)
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($standardOutput) { Write-Host $standardOutput.Trim() }
    if ($standardError) { Write-Host $standardError.Trim() -ForegroundColor Red }
    return $process.ExitCode
}

if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Instalador não encontrado: $installer"
}

if (Test-Path -LiteralPath $testRoot) {
    $resolvedTest = [IO.Path]::GetFullPath($testRoot)
    $resolvedInstaller = [IO.Path]::GetFullPath($installerRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTest.StartsWith($resolvedInstaller, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Diretório de teste fora do instalador: $resolvedTest"
    }
    Remove-Item -LiteralPath $resolvedTest -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $win64 | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $win64 'Palworld-Win64-Shipping.exe') | Out-Null

$installExitCode = Invoke-Installer @('--install', (Join-Path $testRoot 'SteamLibrary'))
if ($installExitCode -ne 0) { throw "Teste de instalação falhou: $installExitCode" }

foreach ($required in @(
    (Join-Path $win64 'dwmapi.dll'),
    (Join-Path $win64 'ue4ss\UE4SS.dll'),
    (Join-Path $win64 'ue4ss\Mods\HoverTransfer\enabled.txt'),
    (Join-Path $win64 'ue4ss\Mods\HoverTransfer\Scripts\main.lua'),
    (Join-Path $win64 'ue4ss\Mods\HoverTransfer\install-manifest.json')
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Arquivo não instalado: $required"
    }
}

$uninstallExitCode = Invoke-Installer @('--uninstall', $fakeGame)
if ($uninstallExitCode -ne 0) { throw "Teste de remoção falhou: $uninstallExitCode" }
if (Test-Path -LiteralPath (Join-Path $win64 'ue4ss\Mods\HoverTransfer')) {
    throw 'A pasta do mod continuou presente após a remoção.'
}
if (-not (Test-Path -LiteralPath (Join-Path $win64 'ue4ss\UE4SS.dll') -PathType Leaf)) {
    throw 'A remoção apagou indevidamente o UE4SS.'
}

Write-Output 'Teste completo do instalador concluído.'
