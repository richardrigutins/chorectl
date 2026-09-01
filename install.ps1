#Requires -Version 5.1
<#
.SYNOPSIS
Installs chorectl for the current user, no admin rights required.
.EXAMPLE
irm https://raw.githubusercontent.com/richardrigutins/chorectl/main/install.ps1 | iex
#>
$ErrorActionPreference = 'Stop'

$Repo = 'richardrigutins/chorectl'
$BinName = 'chorectl.exe'
$InstallDir = if ($env:CHORECTL_INSTALL_DIR) { $env:CHORECTL_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'chorectl' }

if (-not [Environment]::Is64BitOperatingSystem) {
    Write-Error "chorectl: unsupported architecture (32-bit Windows is not supported)"
    exit 1
}

$asset = 'chorectl-win-x64.zip'
$url = "https://github.com/$Repo/releases/latest/download/$asset"

$tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $tmpDir | Out-Null

try {
    $zipPath = Join-Path $tmpDir $asset
    Write-Host "Downloading $asset..."
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing

    Expand-Archive -Path $zipPath -DestinationPath $tmpDir -Force

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Move-Item -Path (Join-Path $tmpDir $BinName) -Destination (Join-Path $InstallDir $BinName) -Force

    Write-Host "Installed chorectl to $(Join-Path $InstallDir $BinName)"

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $pathEntries = $userPath -split ';' | Where-Object { $_ }
    if ($pathEntries -notcontains $InstallDir) {
        [Environment]::SetEnvironmentVariable('Path', "$userPath;$InstallDir", 'User')
        Write-Host ""
        Write-Host "Added $InstallDir to your user PATH. Restart your terminal to use 'chorectl'."
    }
}
finally {
    Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
}
