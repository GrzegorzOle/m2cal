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

Otwórz **`m2cal.appinstaller`** — nie sam plik `.msix`. Wybór ma znaczenie: tylko
instalacja przez `.appinstaller` włącza automatyczne sprawdzanie aktualizacji.

Można kliknąć plik dwukrotnie albo wskazać adres bezpośrednio:

```powershell
Add-AppxPackage -AppInstallerFile https://github.com/GrzegorzOle/m2cal/releases/latest/download/m2cal.appinstaller
```

Aplikacja pojawi się w menu Start jako **m2cal — wzorcowanie toru** i wystartuje
na pełnym ekranie. Wyjście z trybu pełnoekranowego: **Shift + Win + Enter** albo
najechanie kursorem na górną krawędź ekranu.

## 3. Zanim zaczniesz wzorcowanie

Przeczytaj **`INSTRUKCJA_OPERATORA.md`** — dołączona do tego samego wydania.
Opisuje każde pole formularza, jednostki i sposób wpisywania wartości, a także to,
co trzeba przygotować przed sesją (głośność endpointu, pozycja pokręteł M2,
sprawdzenie kalibratorem).

Pozostałe załączniki wydania:

| Plik | Do czego służy |
| --- | --- |
| `INSTRUKCJA_OPERATORA.md` | Obsługa aplikacji przy wzorcowaniu. **Zacznij od tego.** |
| `FORMAT_PLIKU.md` | Format pliku kalibracyjnego — dla autorów aplikacji docelowej i do opisu metody w publikacji. |
| `wzorzec-pliku-kalibracyjnego.json` | Wzorzec z kompletem pól, generowany z kodu narzędzia. |

## 4. Aktualizacja

Dzieje się sama. Przy każdym uruchomieniu Windows sprawdza, czy w wydaniach jest
nowsza wersja, i **pyta operatora o zgodę** przed jej zainstalowaniem.

Monit jest włączony celowo, a nie z ostrożności technicznej: zmiana wersji syntezy
bodźców unieważnia plik kalibracyjny. Operator ma się dowiedzieć, że wersja się
zmieniła, zamiast odkryć to dopiero wtedy, gdy bramka dopuszczenia odrzuci
kalibrację w środku serii badań.

Certyfikatu nie trzeba instalować ponownie, dopóki nie wygaśnie.

**Stanowiska zainstalowane wcześniej z samego pliku `.msix` nie będą się
aktualizować.** Trzeba je raz przeinstalować z `.appinstaller`; danych to nie
narusza, bo tożsamość pakietu się nie zmienia.

## 5. Odinstalowanie

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
