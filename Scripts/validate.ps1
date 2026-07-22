param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$luaParserPython = 'C:\Users\Vini\Desktop\Projetos\Palworld-TrabalhoContinuo\.venv\Scripts\python.exe'

$null = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'build.ps1'),
    [ref]$null,
    [ref]$null
)
$null = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'install.ps1'),
    [ref]$null,
    [ref]$null
)

if (-not (Test-Path -LiteralPath $luaParserPython -PathType Leaf)) {
    throw "Python com luaparser nao encontrado: $luaParserPython"
}

$luaFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.lua' -File
foreach ($file in $luaFiles) {
    & $luaParserPython -c "from pathlib import Path; from luaparser import ast; ast.parse(Path(r'$($file.FullName)').read_text(encoding='utf-8')); print(r'$($file.Name): OK')"
    if ($LASTEXITCODE -ne 0) { throw "Lua invalido: $($file.FullName)" }
}

$transferSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'hover_transfer.lua') -Raw
foreach ($forbiddenPattern in @('FindAllOf\("UserWidget"\)', 'ForEachProperty', 'SendInput', 'mouse_event')) {
    if ($transferSource -match $forbiddenPattern) {
        throw "Padrao inseguro encontrado em hover_transfer.lua: $forbiddenPattern"
    }
}

$sameSlotGuard = $transferSource.IndexOf('buttonAddress == lastProcessedAddress')
$rightClickCall = $transferSource.IndexOf('activeList:OnRightClicked_Internal')
if ($sameSlotGuard -lt 0 -or $rightClickCall -lt 0 -or $sameSlotGuard -gt $rightClickCall) {
    throw 'A protecao contra repeticao precisa ocorrer antes da transferencia.'
}

foreach ($requiredToken in @(
    'WBP_IngameMenu_Chest_C',
    'WBP_IngameMenu_ChestManage_C',
    'WBP_PalItemScrollList',
    'WBP_PalPlayerInventoryScrollList',
    'CachedNowHoveringSlotButton',
    'WBP_IngameMenu_ItemSearchList',
    'RequestMoveItemToInventory'
)) {
    if (-not $transferSource.Contains($requiredToken)) {
        throw "Integracao esperada ausente: $requiredToken"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'enabled.txt') -PathType Leaf)) {
    throw 'enabled.txt ausente.'
}

Write-Output 'Validacao concluida.'
