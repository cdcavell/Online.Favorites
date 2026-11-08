#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Publishes Online.Favorites and runs it as a Windows service on https://localhost (no IIS).

.DESCRIPTION
    Safe to re-run for upgrades: it stops the service, republishes, and starts it again.
    The database lives in %ProgramData%\Online.Favorites\favorites.db and is never overwritten.

.PARAMETER LegacyDatabase
    Optional path to the old Customizable.Menu Application.db. Its menus, sites and links are
    copied into the new database (read-only on the old file; only runs while the new one is empty).

.EXAMPLE
    .\scripts\Install-Service.ps1 -LegacyDatabase 'D:\GitHub\CustomizedMenu\Customizable.Menu\Data\Application.db'
#>
param(
    [string] $InstallDir = (Join-Path $env:ProgramFiles 'Online.Favorites'),
    [string] $LegacyDatabase,
    [int] $Port = 443
)

$ErrorActionPreference = 'Stop'
$serviceName = 'Online.Favorites'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src\Online.Favorites\Online.Favorites.csproj'
$exe = Join-Path $InstallDir 'Online.Favorites.exe'

$service = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') {
    Write-Host 'Stopping the running service...'
    Stop-Service $serviceName
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

# IIS (and anything else using http.sys) shows up as the System process, PID 4.
$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $owner = if ($listener.OwningProcess -eq 4) { 'IIS / http.sys (System)' } else { (Get-Process -Id $listener.OwningProcess).ProcessName }
    throw "Port $Port is already in use by $owner. Remove the IIS https binding for localhost (IIS Manager > Sites > Bindings) or stop that site, then run this again."
}

& (Join-Path $PSScriptRoot 'New-LocalhostCertificate.ps1')

Write-Host "Publishing to $InstallDir..."
# The win-x64 single-file restore differs from CI's, so don't let it rewrite packages.lock.json.
dotnet publish $project -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:RestorePackagesWithLockFile=false -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

if ($Port -ne 443) {
    # appsettings.Production.json overrides appsettings.json and survives republishing only if you keep it.
    @{ Hosting = @{ HttpsPort = $Port } } | ConvertTo-Json | Set-Content (Join-Path $InstallDir 'appsettings.Production.json')
}

if ($LegacyDatabase) {
    Write-Host 'Importing the legacy Customizable.Menu database...'
    & $exe --import $LegacyDatabase
    if ($LASTEXITCODE -ne 0) { Write-Warning 'Import did not run (see the message above). Continuing with the install.' }
}

if (-not $service) {
    New-Service -Name $serviceName `
        -BinaryPathName "`"$exe`"" `
        -DisplayName 'Online Favorites' `
        -Description 'Personal favorites menu on https://localhost.' `
        -StartupType Automatic | Out-Null
    sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/5000/restart/60000 | Out-Null
}

Start-Service $serviceName
$url = if ($Port -eq 443) { 'https://localhost' } else { "https://localhost:$Port" }
Write-Host "Online.Favorites is running at $url"
