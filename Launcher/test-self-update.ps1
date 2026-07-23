param(
    [string]$LauncherVersion = "1.4.0"
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

$installRoot = Join-Path $testRoot "installation"
$installInfo = [Diagnostics.ProcessStartInfo]::new()
$installInfo.FileName = $launcher
$installInfo.Arguments = '"--install-launcher-test" "' + $installRoot.Replace('"', '\"') + '"'
$installInfo.UseShellExecute = $false
$installInfo.CreateNoWindow = $true
$installProcess = [Diagnostics.Process]::Start($installInfo)
$installProcess.WaitForExit()
if ($installProcess.ExitCode -ne 0) { throw "Instalação local do launcher falhou: $($installProcess.ExitCode)" }

$installedExecutable = Join-Path $installRoot "AppData\Palworld-Modpack-Launcher.exe"
$desktopShortcut = Join-Path $installRoot "Desktop\Palworld Modpack Launcher.lnk"
$startMenuShortcut = Join-Path $installRoot "StartMenu\Palworld Modpack Launcher.lnk"
foreach ($path in @($installedExecutable, $desktopShortcut, $startMenuShortcut)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Arquivo da instalação ausente: $path" }
}
$installedHash = (Get-FileHash -LiteralPath $installedExecutable -Algorithm SHA256).Hash
if ($sourceHash -ne $installedHash) { throw "O launcher instalado não corresponde ao executável original." }
$shortcutShell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($desktopShortcut, $startMenuShortcut)) {
    $shortcut = $shortcutShell.CreateShortcut($shortcutPath)
    if ([IO.Path]::GetFullPath($shortcut.TargetPath) -ne [IO.Path]::GetFullPath($installedExecutable)) {
        throw "O atalho aponta para um executável incorreto: $shortcutPath"
    }
}

Write-Host "Teste de autoatualização do launcher concluído."
