param([switch]$TestOnly)
$ErrorActionPreference = 'Stop'
& dotnet run --project (Join-Path $PSScriptRoot 'Camomille.Tests/Camomille.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
if (!$TestOnly) {
    $publishDirectory = Join-Path $PSScriptRoot 'artifacts/Mint'
    & dotnet publish (Join-Path $PSScriptRoot 'Universal x86 Tuning Utility/Universal x86 Tuning Utility.csproj') -c Release -r win-x64 --self-contained true -o $publishDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $publishDirectory 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE-LibreHardwareMonitor.txt') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $publishDirectory
}
