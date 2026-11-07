#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Creates (once) the HTTPS certificate Online.Favorites uses for https://localhost and trusts it.

.DESCRIPTION
    Makes a self-signed certificate for "localhost" in LocalMachine\My with the friendly name
    "Online.Favorites" (the app looks it up by that name), then adds its public part to
    LocalMachine\Root so Edge and Chrome trust it. Does nothing if a valid one already exists.
#>
param(
    [int] $Years = 5
)

$ErrorActionPreference = 'Stop'
$friendlyName = 'Online.Favorites'

$existing = Get-ChildItem Cert:\LocalMachine\My |
    Where-Object { $_.FriendlyName -eq $friendlyName -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($existing) {
    Write-Host "Using existing certificate $($existing.Thumbprint) (expires $($existing.NotAfter.ToShortDateString()))."
    return
}

$certificate = New-SelfSignedCertificate `
    -DnsName 'localhost' `
    -FriendlyName $friendlyName `
    -CertStoreLocation Cert:\LocalMachine\My `
    -NotAfter (Get-Date).AddYears($Years) `
    -KeyAlgorithm RSA -KeyLength 2048 `
    -KeyExportPolicy NonExportable

$publicPart = Join-Path $env:TEMP 'online-favorites-localhost.cer'
try {
    Export-Certificate -Cert $certificate -FilePath $publicPart | Out-Null
    Import-Certificate -FilePath $publicPart -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
}
finally {
    Remove-Item $publicPart -ErrorAction SilentlyContinue
}

Write-Host "Created and trusted certificate $($certificate.Thumbprint) for https://localhost."
