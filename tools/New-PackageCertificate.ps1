<#
.SYNOPSIS
    Tworzy certyfikat własny do podpisywania pakietu MSIX.

.DESCRIPTION
    Podmiot certyfikatu musi być identyczny z atrybutem Publisher w
    src/M2Cal.Uwp/Package.appxmanifest — inaczej podpisany pakiet nie przejdzie
    instalacji. Skrypt sprawdza tę zgodność, zanim cokolwiek wygeneruje.

    Powstają dwa pliki:
      *.pfx  klucz prywatny — do GitHub Secrets, NIGDY do repozytorium
      *.cer  część publiczna — do zainstalowania na stanowisku jako zaufana

.EXAMPLE
    .\tools\New-PackageCertificate.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts'),
    [int]    $ValidYears = 3,
    [string] $ManifestPath = (Join-Path $PSScriptRoot '..\src\M2Cal.Uwp\Package.appxmanifest')
)

$ErrorActionPreference = 'Stop'

# Podmiot bierzemy z manifestu, żeby nie dało się ich rozjechać.
[xml] $manifest = Get-Content -Path $ManifestPath
$subject = $manifest.Package.Identity.Publisher
if ([string]::IsNullOrWhiteSpace($subject)) {
    throw "Nie udało się odczytać atrybutu Publisher z $ManifestPath"
}

Write-Host "Podmiot certyfikatu (z manifestu): $subject"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$password = Read-Host -Prompt 'Hasło do klucza prywatnego (.pfx)' -AsSecureString
if ($password.Length -eq 0) { throw 'Hasło nie może być puste.' }

$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $subject `
    -FriendlyName 'm2cal - podpisywanie pakietu' `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears($ValidYears) `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

$pfxPath = Join-Path $OutputDirectory 'm2cal-signing.pfx'
$cerPath = Join-Path $OutputDirectory 'm2cal-signing.cer'

Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $password | Out-Null
Export-Certificate  -Cert $certificate -FilePath $cerPath | Out-Null

$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath))
$base64Path = Join-Path $OutputDirectory 'm2cal-signing.pfx.base64.txt'
Set-Content -Path $base64Path -Value $base64 -NoNewline

Write-Host ''
Write-Host 'Gotowe.'
Write-Host "  odcisk palca : $($certificate.Thumbprint)"
Write-Host "  wazny do     : $($certificate.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host "  klucz        : $pfxPath"
Write-Host "  czesc jawna  : $cerPath"
Write-Host ''
Write-Host 'Do ustawien repozytorium (Settings > Secrets and variables > Actions) dodaj:'
Write-Host "  SIGNING_CERTIFICATE_BASE64    <- zawartosc $base64Path"
Write-Host '  SIGNING_CERTIFICATE_PASSWORD  <- podane wyzej haslo'
Write-Host ''
Write-Warning 'Pliku .pfx ani .base64.txt nie commituj. Katalog artifacts/ jest wykluczony z repozytorium.'
