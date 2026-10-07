param([string]$IsccPath, [string]$OutputDirectory = 'artifacts/installer')
$ErrorActionPreference = 'Stop'

if (!$IsccPath) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { $IsccPath = $compiler.Source }
    else {
        $candidates = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
        )
        $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (!$IsccPath -or !(Test-Path -LiteralPath $IsccPath)) {
    throw 'Install Inno Setup 6.4 or later, or pass -IsccPath with the path to ISCC.exe.'
}

[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/Mint/Mint.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$payload = 'artifacts/installer-payload-' + [Guid]::NewGuid().ToString('N')
& (Join-Path $PSScriptRoot 'Build-Mint.ps1') -OutputDirectory $payload

$publishDirectory = Join-Path $PSScriptRoot $payload
$installerDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory))
New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null
& $IsccPath "/DAppVersion=$version" "/DPublishDir=$publishDirectory" "/DInstallerDir=$installerDirectory" (Join-Path $PSScriptRoot 'installer/Mint.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

$installer = Join-Path $installerDirectory "Mint-$version-Setup-x64.exe"
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii
Write-Host "Installer: $installer"
