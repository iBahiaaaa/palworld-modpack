param()

$ErrorActionPreference = 'Stop'
$installerRoot = $PSScriptRoot
$project = Join-Path $installerRoot 'HoverTransferInstaller\HoverTransferInstaller.csproj'
$releaseRoot = Join-Path (Split-Path -Parent $installerRoot) 'Releases'
$publishRoot = Join-Path $installerRoot 'publish'
$finalName = 'HoverTransfer-Instalador-v0.3.0.exe'
$finalPath = Join-Path $releaseRoot $finalName

& (Join-Path $installerRoot 'prepare-payload.ps1')

if (Test-Path -LiteralPath $publishRoot) {
    $resolvedPublish = [IO.Path]::GetFullPath($publishRoot)
    $resolvedInstaller = [IO.Path]::GetFullPath($installerRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPublish.StartsWith($resolvedInstaller, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Diretório de publicação fora do instalador: $resolvedPublish"
    }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}

dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou: $LASTEXITCODE" }

$publishedExe = Join-Path $publishRoot 'HoverTransfer-Instalador.exe'
if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
    throw "Executável publicado não encontrado: $publishedExe"
}

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
Copy-Item -LiteralPath $publishedExe -Destination $finalPath -Force

$verifyInfo = [Diagnostics.ProcessStartInfo]::new($finalPath)
$verifyInfo.UseShellExecute = $false
$verifyInfo.RedirectStandardOutput = $true
$verifyInfo.RedirectStandardError = $true
$verifyInfo.Arguments = '--verify'
$verifyProcess = [Diagnostics.Process]::Start($verifyInfo)
$verifyOutput = $verifyProcess.StandardOutput.ReadToEnd()
$verifyError = $verifyProcess.StandardError.ReadToEnd()
$verifyProcess.WaitForExit()
if ($verifyOutput) { Write-Output $verifyOutput.Trim() }
if ($verifyProcess.ExitCode -ne 0) {
    throw "A validação interna do instalador falhou: $verifyError"
}

$hash = (Get-FileHash -LiteralPath $finalPath -Algorithm SHA256).Hash
$hashPath = "$finalPath.sha256.txt"
Set-Content -LiteralPath $hashPath -Encoding UTF8 -Value "$hash  $finalName"

Write-Output "Instalador criado: $finalPath"
Write-Output "Tamanho: $((Get-Item $finalPath).Length) bytes"
Write-Output "SHA256: $hash"
