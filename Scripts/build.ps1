param()

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'Native\hover_transfer_keys.c'
$scriptsDirectory = Join-Path $projectRoot 'Scripts'
$outputDll = Join-Path $scriptsDirectory 'HoverTransferKeys.dll'
$buildDirectory = Join-Path $projectRoot 'build'

$vswhereCandidates = @(
    (Get-Command vswhere.exe -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Source),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -Unique
$vswhere = $vswhereCandidates | Select-Object -First 1
if (-not $vswhere) { throw 'Visual Studio com ferramentas C++ nao foi encontrado.' }

$vsInstall = (& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()
if ([string]::IsNullOrWhiteSpace($vsInstall)) { throw 'Toolchain C++ x64 nao encontrado.' }

$devShellModule = Join-Path $vsInstall 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll'
Import-Module $devShellModule
Enter-VsDevShell -VsInstallPath $vsInstall -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64 -vcvars_ver=14.44' | Out-Null

New-Item -ItemType Directory -Force -Path $buildDirectory, $scriptsDirectory | Out-Null
$compiler = (Get-Command cl.exe -ErrorAction Stop).Source
$arguments = @(
    '/nologo', '/TC', '/LD', '/O2', '/MT', '/GS', '/guard:cf', '/W4', '/WX', '/Brepro',
    '/DWIN32_LEAN_AND_MEAN', '/DNDEBUG', $source,
    '/link', '/NOLOGO', '/MACHINE:X64', '/DYNAMICBASE', '/NXCOMPAT', '/GUARD:CF', '/BREPRO',
    '/INCREMENTAL:NO', "/OUT:$outputDll",
    "/IMPLIB:$(Join-Path $buildDirectory 'hover_transfer_keys.lib')",
    "/PDB:$(Join-Path $buildDirectory 'hover_transfer_keys.pdb')",
    'User32.lib', 'Kernel32.lib'
)

Push-Location $buildDirectory
try {
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o auxiliar nativo: $LASTEXITCODE" }
}
finally {
    Pop-Location
}

Write-Output "Compilado: $outputDll"
Write-Output "SHA256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $outputDll).Hash)"

