param(
    [string]$Version = '0.4.0',
    [string]$PythonExe,
    [switch]$SkipValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$mainLua = Join-Path $projectRoot 'Scripts\main.lua'
$pythonPackager = Join-Path $PSScriptRoot 'package_release.py'
$releaseDirectory = Join-Path $projectRoot (Join-Path 'dist' ("v{0}" -f $Version))
$archiveName = "Keep-Working-While-Alt-Tabbed-$Version.zip"
$archivePath = Join-Path $releaseDirectory $archiveName
$checksumPath = Join-Path $releaseDirectory 'SHA256SUMS.txt'
$reproA = Join-Path $releaseDirectory '.repro-a.zip.tmp'
$reproB = Join-Path $releaseDirectory '.repro-b.zip.tmp'

foreach ($requiredFile in @($mainLua, $pythonPackager)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Missing packaging input: $requiredFile"
    }
}
$mainLuaText = Get-Content -Raw -LiteralPath $mainLua
$versionMatch = [regex]::Match($mainLuaText, 'local MOD_VERSION = "([^"]+)"')
if (-not $versionMatch.Success) {
    throw 'Could not read MOD_VERSION from Scripts\main.lua.'
}
if ($versionMatch.Groups[1].Value -ne $Version) {
    throw "Package version mismatch: requested=$Version source=$($versionMatch.Groups[1].Value)"
}

if (-not $SkipValidation) {
    & (Join-Path $PSScriptRoot 'validate.ps1') -PythonExe $PythonExe
    if ($LASTEXITCODE -ne 0) {
        throw "Static validation failed with exit code $LASTEXITCODE."
    }
}

$pythonCommand = $null
$pythonArguments = @()
if ($PythonExe) {
    if (Test-Path -LiteralPath $PythonExe -PathType Leaf) {
        $pythonCommand = (Resolve-Path -LiteralPath $PythonExe).Path
    }
    else {
        $pythonOverride = Get-Command $PythonExe -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $pythonOverride) {
            throw "Python override was not found: $PythonExe"
        }
        $pythonCommand = $pythonOverride.Source
    }
}
else {
    $venvPython = Join-Path $projectRoot '.venv\Scripts\python.exe'
    if (Test-Path -LiteralPath $venvPython -PathType Leaf) {
        $pythonCommand = $venvPython
    }
    else {
        $pythonLauncher = Get-Command py.exe -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($pythonLauncher) {
            $pythonCommand = $pythonLauncher.Source
            $pythonArguments = @('-3')
        }
        else {
            $pythonFallback = Get-Command python.exe, python3.exe -CommandType Application -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($pythonFallback) {
                $pythonCommand = $pythonFallback.Source
            }
        }
    }
}

if (-not $pythonCommand) {
    throw 'Python 3 was not found. Create .venv and install requirements-dev.txt.'
}

$pythonVersion = & $pythonCommand @pythonArguments --version 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($pythonVersion -join ' ').Contains('Python 3')) {
    throw "Python 3 failed its version check: $($pythonVersion -join [Environment]::NewLine)"
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$allowedExistingNames = @($archiveName, 'SHA256SUMS.txt', '.repro-a.zip.tmp', '.repro-b.zip.tmp')
$unexpected = @(
    Get-ChildItem -Force -LiteralPath $releaseDirectory |
        Where-Object { $_.Name -notin $allowedExistingNames }
)
if ($unexpected.Count -gt 0) {
    throw "Release directory contains unexpected files: $($unexpected.Name -join ', ')"
}

foreach ($knownOutput in @($archivePath, $checksumPath, $reproA, $reproB)) {
    if (Test-Path -LiteralPath $knownOutput) {
        Remove-Item -Force -LiteralPath $knownOutput
    }
}

try {
    foreach ($temporaryArchive in @($reproA, $reproB)) {
        $packagerOutput = & $pythonCommand @pythonArguments $pythonPackager $projectRoot $temporaryArchive 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Archive build failed:`n$($packagerOutput -join [Environment]::NewLine)"
        }
        $packagerOutput | Write-Output
    }

    $reproAHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $reproA).Hash
    $reproBHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $reproB).Hash
    if ($reproAHash -ne $reproBHash) {
        throw "Release archives are not reproducible: first=$reproAHash second=$reproBHash"
    }

    Move-Item -LiteralPath $reproA -Destination $archivePath
    Remove-Item -Force -LiteralPath $reproB

    $archiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    $checksumLine = "$archiveHash  $archiveName`n"
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($checksumPath, $checksumLine, $utf8NoBom)

    if ([System.IO.File]::ReadAllText($checksumPath, $utf8NoBom) -cne $checksumLine) {
        throw 'SHA256SUMS.txt round-trip verification failed.'
    }

    $finalNames = @(Get-ChildItem -Force -LiteralPath $releaseDirectory | ForEach-Object { $_.Name })
    $expectedFinalNames = @($archiveName, 'SHA256SUMS.txt')
    $sortedFinalNames = (($finalNames | Sort-Object) -join '|')
    $sortedExpectedFinalNames = (($expectedFinalNames | Sort-Object) -join '|')
    if ($sortedFinalNames -cne $sortedExpectedFinalNames) {
        throw "Unexpected final release outputs: $($finalNames -join ', ')"
    }

    Write-Output "Release archive: $archivePath"
    Write-Output "SHA256: $archiveHash"
    Write-Output "Checksums: $checksumPath"
}
finally {
    foreach ($temporary in @($reproA, $reproB)) {
        if (Test-Path -LiteralPath $temporary) {
            Remove-Item -Force -LiteralPath $temporary
        }
    }
}
