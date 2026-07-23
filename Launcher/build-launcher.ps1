param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot "PalworldModpackLauncher\PalworldModpackLauncher.csproj"
$publish = Join-Path $PSScriptRoot "publish"
$releases = Join-Path $projectRoot "Releases"

dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish /p:Version=$Version /p:FileVersion="$Version.0"

New-Item -ItemType Directory -Force -Path $releases | Out-Null
$source = Join-Path $publish "Palworld-Modpack-Launcher.exe"
$versioned = Join-Path $releases "Palworld-Modpack-Launcher-v$Version.exe"
$stable = Join-Path $releases "Palworld-Modpack-Launcher.exe"
Copy-Item -LiteralPath $source -Destination $versioned -Force
Copy-Item -LiteralPath $source -Destination $stable -Force

$hash = (Get-FileHash -LiteralPath $stable -Algorithm SHA256).Hash
Set-Content -LiteralPath "$stable.sha256" -Value "$hash  Palworld-Modpack-Launcher.exe" -Encoding ascii
$manifest = [ordered]@{ version = $Version }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releases 'launcher-version.json') -Encoding utf8
Write-Host "Launcher gerado: $stable"
Write-Host "SHA256: $hash"
