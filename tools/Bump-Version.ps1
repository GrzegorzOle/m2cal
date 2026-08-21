<#
.SYNOPSIS
    Podnosi numer wersji aplikacji.

.DESCRIPTION
    Wersja żyje w dwóch miejscach — Package.appxmanifest i AssemblyInfo.cs — i musi
    być w nich identyczna. Ten skrypt zmienia oba naraz, żeby nie dało się ich rozjechać.

    Bez parametrów podnosi ostatni człon (rewizję). Windows odmawia instalacji pakietu
    o numerze niższym lub równym już zainstalowanemu, więc każde wydanie musi mieć
    numer wyższy od poprzedniego.

.PARAMETER Part
    Który człon podnieść: Major, Minor, Build albo Revision (domyślnie).
    Człony niższego rzędu są zerowane.

.PARAMETER Version
    Ustawia konkretny numer zamiast podnoszenia, np. 1.2.0.0.

.EXAMPLE
    .\tools\Bump-Version.ps1
    1.1.0.1 -> 1.1.0.2

.EXAMPLE
    .\tools\Bump-Version.ps1 -Part Minor
    1.1.0.2 -> 1.2.0.0

.EXAMPLE
    .\tools\Bump-Version.ps1 -Version 2.0.0.0
#>
[CmdletBinding(DefaultParameterSetName = 'Bump')]
param(
    [Parameter(ParameterSetName = 'Bump')]
    [ValidateSet('Major', 'Minor', 'Build', 'Revision')]
    [string] $Part = 'Revision',

    [Parameter(ParameterSetName = 'Set', Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string] $Version,

    [string] $ManifestPath   = (Join-Path $PSScriptRoot '..\src\M2Cal.Uwp\Package.appxmanifest'),
    [string] $AssemblyInfoPath = (Join-Path $PSScriptRoot '..\src\M2Cal.Uwp\Properties\AssemblyInfo.cs')
)

$ErrorActionPreference = 'Stop'

# Manifest czytamy i zapisujemy jako tekst, a nie przez XmlDocument: zapis XML-em splaszcza
# formatowanie calego pliku i kazda zmiana wersji dawalaby ogromna roznice w historii.
$manifestText = Get-Content -Path $ManifestPath -Raw
# Negatywne spojrzenie wstecz odsiewa MinVersion i MaxVersionTested, ktore tez zawieraja
# slowo "Version" i numer o czterech czlonach.
$versionPattern = '(?<prefix>(?<![A-Za-z])Version\s*=\s*")(?<value>\d+\.\d+\.\d+\.\d+)(?<suffix>")'

$match = [regex]::Match($manifestText, $versionPattern)
if (-not $match.Success) {
    throw "Nie znaleziono atrybutu Version w $ManifestPath"
}
$current = $match.Groups['value'].Value

if ($PSCmdlet.ParameterSetName -eq 'Set') {
    $next = $Version
}
else {
    $v = [version] $current
    switch ($Part) {
        'Major'    { $next = '{0}.0.0.0' -f ($v.Major + 1) }
        'Minor'    { $next = '{0}.{1}.0.0' -f $v.Major, ($v.Minor + 1) }
        'Build'    { $next = '{0}.{1}.{2}.0' -f $v.Major, $v.Minor, ($v.Build + 1) }
        'Revision' { $next = '{0}.{1}.{2}.{3}' -f $v.Major, $v.Minor, $v.Build, ($v.Revision + 1) }
    }
}

if ([version] $next -le [version] $current) {
    throw "Nowa wersja $next nie jest wyzsza od $current. Windows odmowilby instalacji takiego pakietu."
}

$manifestText = [regex]::Replace($manifestText, $versionPattern, "`${prefix}$next`${suffix}", 1)
Set-Content -Path $ManifestPath -Value $manifestText -NoNewline

$assemblyInfo = Get-Content -Path $AssemblyInfoPath -Raw
$assemblyInfo = $assemblyInfo -replace 'AssemblyVersion\("[\d\.]+"\)',     "AssemblyVersion(`"$next`")"
$assemblyInfo = $assemblyInfo -replace 'AssemblyFileVersion\("[\d\.]+"\)', "AssemblyFileVersion(`"$next`")"
Set-Content -Path $AssemblyInfoPath -Value $assemblyInfo -NoNewline

Write-Host "$current -> $next"
Write-Host ''
Write-Host 'Wydanie:'
Write-Host "  git commit -am 'Wersja $next'"
Write-Host "  git tag v$next && git push && git push origin v$next"
