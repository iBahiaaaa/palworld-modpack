param(
    [Parameter(Mandatory = $true)]
    [string]$ModpackVersion,
    [string]$LauncherVersion = "1.3.0",
    [string]$GameRoot,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$gh = Join-Path $env:ProgramFiles "GitHub CLI\gh.exe"
$repository = "iBahiaaaa/palworld-modpack"
$tag = "v$ModpackVersion"

if (-not (Test-Path -LiteralPath $gh)) { throw "GitHub CLI não encontrado." }
& $gh auth status | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Faça login com: gh auth login" }

if (-not $SkipBuild) {
    $packageArguments = @{ Version = $ModpackVersion }
    if (-not [string]::IsNullOrWhiteSpace($GameRoot)) { $packageArguments.GameRoot = $GameRoot }
    & (Join-Path $PSScriptRoot "build-modpack-package.ps1") @packageArguments
    & (Join-Path $projectRoot "Launcher\build-launcher.ps1") -Version $LauncherVersion
    & (Join-Path $projectRoot "Launcher\test-launcher.ps1") -ModpackVersion $ModpackVersion -LauncherVersion $LauncherVersion
}

$releases = Join-Path $projectRoot "Releases"
$assets = @(
    (Join-Path $releases "Palworld-Modpack-Launcher.exe"),
    (Join-Path $releases "Palworld-Modpack-Launcher.exe.sha256"),
    (Join-Path $releases "launcher-version.json"),
    (Join-Path $releases "palworld-modpack.zip"),
    (Join-Path $releases "palworld-modpack.zip.sha256")
)
foreach ($asset in $assets) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) { throw "Asset ausente: $asset" }
}

$releaseCheck = Start-Process -FilePath $gh `
    -ArgumentList @("release", "view", $tag, "--repo", $repository) `
    -Wait -PassThru -WindowStyle Hidden
if ($releaseCheck.ExitCode -eq 0) { throw "A Release $tag já existe. Use uma versão nova." }

$notes = @"
## Palworld Modpack $ModpackVersion

- Atualização automática pelo launcher.
- Autoatualização do próprio launcher incluída.
- O launcher pode atualizar seu executável mesmo com o Palworld aberto.
- Steam inicia o Palworld vanilla; o launcher ativa os mods somente durante a sessão.
- Opção para remover o modpack com backup automático.
- Hover Transfer incluído.
- AltTab Work Continuation incluído.
- Accessory Slots Research incluído com 10 slots totais.
- UE4SS incluído no pacote de cliente.
- Verificação de integridade por SHA-256.

Baixe **Palworld-Modpack-Launcher.exe**, feche o Palworld e execute o launcher.
"@

& $gh release create $tag @assets --repo $repository --title "Palworld Modpack $ModpackVersion" --notes $notes --latest
if ($LASTEXITCODE -ne 0) { throw "Não foi possível publicar a Release $tag." }
Write-Host "Release publicada: https://github.com/$repository/releases/tag/$tag"
