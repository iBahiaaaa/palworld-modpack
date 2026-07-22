param(
    [string]$Version = "0.3.0",
    [string]$GameRoot
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$preparePayload = Join-Path $projectRoot "Installer\prepare-payload.ps1"
$payloadStaging = Join-Path $projectRoot "Installer\payload-staging"
$releaseStaging = Join-Path $PSScriptRoot "staging"
$releases = Join-Path $projectRoot "Releases"
$package = Join-Path $releases "palworld-modpack.zip"

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    & $preparePayload
} else {
    & $preparePayload -GameRoot $GameRoot
}

if (Test-Path -LiteralPath $releaseStaging) {
    $resolved = [IO.Path]::GetFullPath($releaseStaging)
    $allowed = [IO.Path]::GetFullPath($PSScriptRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Pasta temporária insegura: $resolved"
    }
    Remove-Item -LiteralPath $releaseStaging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $releaseStaging, $releases | Out-Null
Copy-Item -Path (Join-Path $payloadStaging "*") -Destination $releaseStaging -Recurse -Force

$stagingPrefix = [IO.Path]::GetFullPath($releaseStaging).TrimEnd('\') + '\'
$files = Get-ChildItem -LiteralPath $releaseStaging -File -Recurse |
    ForEach-Object {
        $fullName = [IO.Path]::GetFullPath($_.FullName)
        if (-not $fullName.StartsWith($stagingPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Arquivo fora da pasta temporária: $fullName"
        }
        $fullName.Substring($stagingPrefix.Length).Replace('\', '/')
    } |
    Sort-Object
$metadata = [ordered]@{
    version = $Version
    files = @($files)
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $releaseStaging "modpack-package.json") -Encoding utf8

if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
Compress-Archive -Path (Join-Path $releaseStaging "*") -DestinationPath $package -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
Set-Content -LiteralPath "$package.sha256" -Value "$hash  palworld-modpack.zip" -Encoding ascii

Write-Host "Pacote gerado: $package"
Write-Host "Arquivos gerenciados: $($files.Count)"
Write-Host "SHA256: $hash"
