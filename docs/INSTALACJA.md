# Instalacja na stanowisku pomiarowym

Pakiet jest podpisany **certyfikatem własnym**. Windows nie zainstaluje takiego
pakietu, dopóki certyfikat nie zostanie uznany za zaufany na danej maszynie —
stąd krok 1. Robi się go raz na komputer.

## 1. Zaufanie certyfikatowi (raz na maszynę, wymaga administratora)

Pobierz z wydania plik `m2cal-signing.cer`, otwórz PowerShell **jako administrator**
i wykonaj:

```powershell
Import-Certificate -FilePath .\m2cal-signing.cer `
                   -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Magazyn **Zaufane osoby** (`TrustedPeople`) to właściwe miejsce dla pakietów
sideloadowanych. Nie instaluj certyfikatu w „Zaufanych głównych urzędach
certyfikacji" — dałoby to certyfikatowi uprawnienia znacznie szersze, niż
potrzeba do zainstalowania jednej aplikacji.

Sprawdzenie:

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -like '*Oleksy*'
```

## 2. Instalacja aplikacji

```powershell
Add-AppxPackage -Path .\M2Cal.Uwp_<wersja>_x64.msix
```

Można też kliknąć plik `.msix` dwukrotnie i użyć Instalatora aplikacji.

Aplikacja pojawi się w menu Start jako **m2cal — wzorcowanie toru**.

## 3. Aktualizacja

Nowsze wydanie instaluje się tą samą komendą — pakiet o tej samej tożsamości
zostanie podmieniony. Certyfikatu nie trzeba instalować ponownie, dopóki nie
wygaśnie ani nie zostanie wymieniony.

## 4. Odinstalowanie

```powershell
Get-AppxPackage -Name eu.cdest.m2cal | Remove-AppxPackage
```

## Rozwiązywanie problemów

**„Nie można zainstalować pakietu, ponieważ certyfikat nie jest zaufany"** —
krok 1 nie został wykonany, został wykonany w magazynie użytkownika zamiast
komputera lokalnego, albo certyfikat wygasł.

**Aplikacja instaluje się, ale nie startuje** — sprawdź plik:

```
%LOCALAPPDATA%\Packages\eu.cdest.m2cal_*\LocalState\startup-error.txt
```

Zapisuje się tam wyjątek startowy wraz ze stosem wywołań. Dziennik zdarzeń
Windows pokazuje w takim wypadku sam kod wyjątku CLR, bez informacji o przyczynie.

**Aplikacja nie widzi MOTU M2** — lista urządzeń pokazuje wszystkie aktywne
wyjścia audio; M2 jest oznaczone gwiazdką i wybierane automatycznie, jeśli zostanie
wykryte. Bez niego wybierz urządzenie ręcznie, ale pamiętaj, że wzorcowanie ma sens
wyłącznie dla toru, którym faktycznie gra aplikacja docelowa.

## Uwaga o ważności wzorcowania

Instalacja nowej wersji aplikacji **nie unieważnia** pliku kalibracyjnego, o ile nie
zmieniła się wersja syntezy bodźców (`synthesizerVersion` w pliku). Jeśli się
zmieniła, bramka dopuszczenia odrzuci plik i wzorcowanie trzeba powtórzyć — jest to
zachowanie zamierzone, opisane w `README.md`.
