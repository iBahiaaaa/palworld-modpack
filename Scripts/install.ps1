param(
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\Palworld'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $GameRoot 'Pal\Binaries\Win64\ue4ss\Mods\HoverTransfer'

& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'A compilacao falhou.' }

New-Item -ItemType Directory -Force -Path (Join-Path $destination 'Scripts') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'enabled.txt') -Destination (Join-Path $destination 'enabled.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'main.lua') -Destination (Join-Path $destination 'Scripts\main.lua') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'config.lua') -Destination (Join-Path $destination 'Scripts\config.lua') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'hover_transfer.lua') -Destination (Join-Path $destination 'Scripts\hover_transfer.lua') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'HoverTransferKeys.dll') -Destination (Join-Path $destination 'Scripts\HoverTransferKeys.dll') -Force

Write-Output "Mod instalado em: $destination"
Write-Output 'Reabra o Palworld para carregar o mod. O servidor nao precisa ser reiniciado.'

