param(
    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$installerProject = Join-Path $PSScriptRoot 'HoverTransferInstaller'
$payloadRoot = Join-Path $PSScriptRoot 'payload-staging'
$payloadZip = Join-Path $installerProject 'Payload.zip'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $steamRoots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($registryPath in @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )) {
        try {
            $steam = Get-ItemProperty -LiteralPath $registryPath -ErrorAction Stop
            foreach ($property in @('SteamPath', 'InstallPath')) {
                if ($steam.$property -and (Test-Path -LiteralPath $steam.$property)) {
                    $steamRoots.Add([IO.Path]::GetFullPath($steam.$property))
                }
            }
        } catch { }
    }

    $libraries = New-Object 'System.Collections.Generic.List[string]'
    foreach ($steamRoot in $steamRoots | Select-Object -Unique) {
        $libraries.Add($steamRoot)
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdf)) { continue }
        $text = Get-Content -LiteralPath $vdf -Raw
        foreach ($match in [regex]::Matches($text, '"path"\s+"(?<path>[^"]+)"')) {
            $library = $match.Groups['path'].Value.Replace('\\', '\')
            if (Test-Path -LiteralPath $library) { $libraries.Add($library) }
        }
    }

    $GameRoot = $libraries |
        ForEach-Object { Join-Path $_ 'steamapps\common\Palworld' } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Pal\Binaries\Win64\Palworld-Win64-Shipping.exe') } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($GameRoot)) {
        throw 'Palworld não encontrado automaticamente. Informe -GameRoot.'
    }
}

$sourceWin64 = Join-Path $GameRoot 'Pal\Binaries\Win64'
$sourceUe4ss = Join-Path $sourceWin64 'ue4ss'
$sourceMod = Join-Path $projectRoot 'Scripts'
$sourceAltTab = Join-Path $projectRoot 'Mods\AltTabWorkContinuation'
$sourceAccessorySlots = Join-Path $projectRoot 'Mods\AccessorySlotsResearch'

foreach ($required in @(
    (Join-Path $sourceWin64 'dwmapi.dll'),
    (Join-Path $sourceUe4ss 'UE4SS.dll'),
    (Join-Path $sourceUe4ss 'UE4SS-settings.ini'),
    (Join-Path $sourceUe4ss 'MemberVariableLayout.ini'),
    (Join-Path $sourceUe4ss 'LICENSE'),
    (Join-Path $sourceMod 'main.lua'),
    (Join-Path $sourceMod 'config.lua'),
    (Join-Path $sourceMod 'hover_transfer.lua'),
    (Join-Path $sourceMod 'HoverTransferKeys.dll'),
    (Join-Path $projectRoot 'enabled.txt'),
    (Join-Path $sourceAltTab 'enabled.txt'),
    (Join-Path $sourceAltTab 'Scripts\main.lua'),
    (Join-Path $sourceAltTab 'Scripts\continuation_policy.lua'),
    (Join-Path $sourceAltTab 'Scripts\AltTabWorkContinuationFocus.dll'),
    (Join-Path $sourceAccessorySlots 'enabled.txt'),
    (Join-Path $sourceAccessorySlots 'Scripts\main.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\config.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\slot_limits.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\equipment_storage.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\slot_expander.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\slot_refresher.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\slot_visual_style.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\slot_probe.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\ui_probe.lua'),
    (Join-Path $sourceAccessorySlots 'Scripts\session_log.lua')
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Arquivo obrigatório ausente: $required"
    }
}

if (Test-Path -LiteralPath $payloadRoot) {
    $resolvedPayload = [IO.Path]::GetFullPath($payloadRoot)
    $resolvedInstaller = [IO.Path]::GetFullPath($PSScriptRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPayload.StartsWith($resolvedInstaller, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Diretório de payload fora do instalador: $resolvedPayload"
    }
    Remove-Item -LiteralPath $resolvedPayload -Recurse -Force
}

$targetUe4ss = Join-Path $payloadRoot 'ue4ss'
$targetMod = Join-Path $targetUe4ss 'Mods\HoverTransfer'
$targetScripts = Join-Path $targetMod 'Scripts'
$targetAltTab = Join-Path $targetUe4ss 'Mods\AltTabWorkContinuation'
$targetAltTabScripts = Join-Path $targetAltTab 'Scripts'
$targetAccessorySlots = Join-Path $targetUe4ss 'Mods\AccessorySlotsResearch'
$targetAccessoryScripts = Join-Path $targetAccessorySlots 'Scripts'
New-Item -ItemType Directory -Force -Path $targetScripts, $targetAltTabScripts, $targetAccessoryScripts | Out-Null

Copy-Item -LiteralPath (Join-Path $sourceWin64 'dwmapi.dll') -Destination (Join-Path $payloadRoot 'dwmapi.dll')
foreach ($name in @('UE4SS.dll', 'UE4SS-settings.ini', 'MemberVariableLayout.ini', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $sourceUe4ss $name) -Destination (Join-Path $targetUe4ss $name)
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'enabled.txt') -Destination (Join-Path $targetMod 'enabled.txt')
foreach ($name in @('main.lua', 'config.lua', 'hover_transfer.lua', 'HoverTransferKeys.dll')) {
    Copy-Item -LiteralPath (Join-Path $sourceMod $name) -Destination (Join-Path $targetScripts $name)
}
Copy-Item -LiteralPath (Join-Path $sourceAltTab 'enabled.txt') -Destination (Join-Path $targetAltTab 'enabled.txt')
foreach ($name in @('main.lua', 'continuation_policy.lua', 'AltTabWorkContinuationFocus.dll')) {
    Copy-Item -LiteralPath (Join-Path $sourceAltTab "Scripts\$name") -Destination (Join-Path $targetAltTabScripts $name)
}
Copy-Item -LiteralPath (Join-Path $sourceAccessorySlots 'enabled.txt') -Destination (Join-Path $targetAccessorySlots 'enabled.txt')
Get-ChildItem -LiteralPath (Join-Path $sourceAccessorySlots 'Scripts') -Filter '*.lua' -File |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetAccessoryScripts $_.Name)
    }

$unexpectedMods = Get-ChildItem -LiteralPath (Join-Path $targetUe4ss 'Mods') -Directory |
    Where-Object Name -notin @('HoverTransfer', 'AltTabWorkContinuation', 'AccessorySlotsResearch')
if ($unexpectedMods) {
    throw "O payload contém mods inesperados: $($unexpectedMods.Name -join ', ')"
}

if (Test-Path -LiteralPath $payloadZip) { Remove-Item -LiteralPath $payloadZip -Force }
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $payloadZip -CompressionLevel Optimal

Write-Output "Payload preparado: $payloadZip"
Write-Output "Tamanho: $((Get-Item $payloadZip).Length) bytes"
Write-Output "SHA256: $((Get-FileHash $payloadZip -Algorithm SHA256).Hash)"
