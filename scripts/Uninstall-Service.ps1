#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Stops and removes the Online.Favorites Windows service.

.DESCRIPTION
    Leaves your data (%ProgramData%\Online.Favorites) in place. Add -RemoveFiles to delete the
    installed program folder and -RemoveCertificate to delete the localhost certificate.
#>
param(
    [string] $InstallDir = (Join-Path $env:ProgramFiles 'Online.Favorites'),
    [switch] $RemoveFiles,
    [switch] $RemoveCertificate
)

$ErrorActionPreference = 'Stop'
$serviceName = 'Online.Favorites'

$service = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') { Stop-Service $serviceName }
    sc.exe delete $serviceName | Out-Null
    Write-Host 'Service removed.'
}

if ($RemoveFiles -and (Test-Path $InstallDir)) {
    Remove-Item $InstallDir -Recurse -Force
    Write-Host "Removed $InstallDir."
}

if ($RemoveCertificate) {
    # The trusted copy in Root has no friendly name, so match it by thumbprint.
    $thumbprints = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.FriendlyName -eq 'Online.Favorites' } | ForEach-Object Thumbprint
    foreach ($thumbprint in $thumbprints) {
        Remove-Item "Cert:\LocalMachine\My\$thumbprint", "Cert:\LocalMachine\Root\$thumbprint" -ErrorAction SilentlyContinue
    }
    Write-Host 'Removed the Online.Favorites certificate.'
}

Write-Host "Your database is still in $(Join-Path $env:ProgramData 'Online.Favorites')."
