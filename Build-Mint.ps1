param([switch]$TestOnly, [string]$OutputDirectory = 'artifacts/Mint')
$ErrorActionPreference = 'Stop'
& dotnet run --project (Join-Path $PSScriptRoot 'tests/Mint.Tests/Mint.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
if (!$TestOnly) {
    $publishDirectory = Join-Path $PSScriptRoot $OutputDirectory
    & dotnet publish (Join-Path $PSScriptRoot 'src/Mint/Mint.csproj') -c Release -r win-x64 --self-contained true -o $publishDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $publishDirectory 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE-LibreHardwareMonitor.txt') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $publishDirectory
}
