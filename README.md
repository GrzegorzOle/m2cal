# motu-m2-cal

Sterownik testowy do **wzorcowania toru DAC MOTU M2** na potrzeby badań słuchowych
(test przesiewowy i test dychotyczny). To narzędzie pomocnicze — nie jest częścią
aplikacji głównej (UWP), ale wytwarza artefakt, z którego aplikacja główna korzysta:
**plik kalibracyjny** (`calibration.m2cal.json`), oraz współdzieli z nią bibliotekę
`M2Cal.Core`.

## Idea

Podczas wzorcowania narzędzie pełni rolę „kodu testowego": odtwarza bodźce odniesienia
przez MOTU M2, a operator mierzy poziom akustyczny na mikrofonach odsłuchowych
(sztuczna głowa / sztuczne ucho z miernikiem klasy 1). Z pomiarów powstaje mapa
`dBFS → dB SPL → dB HL` dla każdego kanału i częstotliwości. Plik kalibracyjny
przenosi się do aplikacji głównej, która przed badaniem **rozpoznaje urządzenie,
sprawdza presety i dopiero wtedy dopuszcza do badania** (klasa `DeviceGate`).

```
┌────────────────────┐   wspólny kod: M2Cal.Core (netstandard2.0)   ┌───────────────────┐
│  m2cal (to repo)   │   synteza bodźców, model kalibracji,         │  Aplikacja UWP    │
│  M2Cal.Uwp — GUI   │   przeliczenia dB HL, DeviceGate             │  (projekt główny) │
│  M2Cal.Cli — CLI   │                                              └─────────▲─────────┘
└─────────┬──────────┘                                                        │ wczytuje
          │  wzorcowanie + verify                                             │
          ▼                                                                   │
   calibration.m2cal.json  ──────────────────────────────────────────────────┘
```

Narzędzie ma dwa interfejsy nad tym samym rdzeniem. **GUI (`M2Cal.Uwp`)** prowadzi
operatora przez pomiary i jest samo aplikacją UWP — bodźce idą przez `AudioGraph`,
czyli dokładnie tą drogą, którą zagra aplikacja docelowa. **CLI (`M2Cal.Cli`)**
zostaje do pracy skryptowej, kontroli `verify` i testów bez sprzętu.

Kluczowa zasada metrologiczna: wzorcowany jest **cały tor w takiej konfiguracji,
w jakiej gra aplikacja** — ten sam endpoint WASAPI, ta sama częstotliwość próbkowania,
ta sama głośność endpointu, te same słuchawki i ta sama pozycja analogowych pokręteł M2.
Dzięki współdzieleniu `ToneSynthesizer` z aplikacją główną wzorcowany jest dokładnie
ten kod DSP, który później generuje bodźce w badaniu.

## Model przeliczeń

Konwencja: sinus pełnej skali = 0 dBFS. Przy wzorcowaniu ton odniesienia `refDbFs`
(domyślnie −20 dBFS) daje zmierzone `SPL_meas(f, kanał)`. Wtedy dla zadanego poziomu
słyszenia HL (dB HL):

```
cel dB SPL = RETSPL(f) + HL                     (RETSPL wg serii ISO 389 dla użytej słuchawki)
dBFS       = refDbFs + (cel dB SPL − SPL_meas)  (CalibrationFile.RequiredDbFs)
```

Przykład: `refDbFs = −20`, zmierzono 74 dB SPL przy 1 kHz, RETSPL(1 kHz) = 7 dB →
20 dB HL wymaga 27 dB SPL → bodziec `−67 dBFS`. Przy typowej czułości toru poziomy
przesiewowe wypadają głęboko pod pełną skalą — dlatego głośność endpointu ma stać
na 100%, a cała regulacja poziomu odbywa się w dBFS.

## Aplikacja wzorcująca (GUI)

`M2Cal.Uwp` to ekran, na którym powstaje mapa pomiarów. Operator ustawia bodziec,
odczytuje wskazanie miernika i zapisuje wiersz:

| Pole | Znaczenie |
| --- | --- |
| **Częstotliwość [Hz]** | częstotliwość nośna bodźca |
| **Poziom sygnału [dBFS]** | poziom podany na wyjście (sinus pełnej skali = 0 dBFS) |
| **Kanał** | L albo P — drugi kanał to cyfrowa cisza |
| **Zmierzony poziom [dB SPL]** | odczyt z miernika klasy 1, wpisywany ręcznie |

Poziom reguluje się wpisem z klawiatury albo przyciskami krokowymi ±1 dB i ±5 dB,
z ograniczeniem do zakresu −120…0 dBFS. Poziom wolno zmieniać **w trakcie grania** —
pole i generator pozostają zgodne, więc wskazanie miernika odpowiada temu, co
zostanie zapisane. Zmiana pozostałych parametrów (częstotliwość, kanał, rodzaj
bodźca) przerywa ton, bo zmienia jego charakter, a nie samą amplitudę.

Pojedynczy pomiar: ustaw bodziec, **Graj**, odczytaj miernik, wpisz wynik w pole
zmierzonego poziomu i kliknij **Dodaj pomiar do mapy**. Wiersz pojawia się w tabeli
po prawej. Dopiero **Zapisz plik kalibracyjny** zapisuje całą mapę na dysk — to
osobna czynność, wykonywana raz, na końcu sesji.

Każdy zapis daje jeden punkt mapy, a z niego wynika **czułość toru** — poziom
akustyczny, jaki dałby sinus pełnej skali (`SPL_zmierzone − dBFS_bodźca`).
Aplikacja pokazuje tę wartość od razu, jeszcze przed zapisem punktu, i ostrzega,
gdy powtórzenia tej samej pary (częstotliwość, kanał) rozjeżdżają się o więcej
niż 2 dB — to zwykle znak przesuniętej słuchawki, a nie nieliniowości toru.

Zapis kilku punktów przy różnych poziomach dla tej samej częstotliwości jest
świadomie dozwolony: pozwala sprawdzić liniowość toru bez osobnego trybu.

Mapa trafia do `calibration.m2cal.json` razem z odciskiem toru i metadanymi sesji
(operator, przetwornik, sprzęgacz, notatki). Przycisk **Selftest przeliczeń**
uruchamia w GUI ten sam zestaw testów co `m2cal selftest` — w kompilacji Release
przechodzi on przez .NET Native, więc potwierdza także, że serializacja pliku
kalibracyjnego działa na maszynie stanowiskowej.

Czego GUI **nie** potwierdzi za operatora: UWP działa w kontenerze aplikacji i nie
odczyta głośności endpointu ani ustawień miksera Windows. Dlatego stan ten
potwierdza się polem wyboru, a bez tego potwierdzenia zapisany odcisk urządzenia
nie przejdzie bramki dopuszczenia. Kontrolę `verify` wykonuje się z CLI.

## Komendy

```
m2cal devices                                # lista urządzeń, MOTU M2 oznaczone ★
m2cal check [--cal plik.json]                # fingerprint + presety; z --cal: bramka dopuszczenia
m2cal tone --freq 1000 --dbfs -20 --ch L     # ton diagnostyczny (też --pulsed, --warble)
m2cal calibrate --freqs 500,1000,2000,4000 --ref-dbfs -20 --transducer "TDH-39"
m2cal verify --cal calibration.m2cal.json --retspl examples/retspl.tdh39.example.json --hl 20
m2cal screen --cal calibration.m2cal.json --retspl ... --hl 20 --freqs 1000,2000
m2cal selftest                               # testy matematyki (bez sprzętu, każdy OS)
```

`calibrate` prowadzi operatora przez wszystkie częstotliwości i kanały (L, P),
`verify` odtwarza poziomy w dB HL i sprawdza je w tolerancji (domyślnie ±3 dB,
jak w wymaganiach dla audiometrów IEC 60645-1 w zakresie 125 Hz–4 kHz), a wynik
zapisuje do pliku kalibracyjnego. `screen` symuluje bodziec przesiewowy dokładnie
tak, jak zrobi to aplikacja główna — domyślnie 1000 i 2000 Hz przy 20 dB HL, ton
pulsowany, osobno na każde ucho.

Kody wyjścia: `0` OK / dopuszczone, `3` bramka odrzuciła, `4` verify negatywny —
nadają się do skryptów.

## Budowanie

Solucja zawiera projekt UWP, więc całość buduje MSBuild z Visual Studio (wymagany
komponent „Universal Windows Platform development" i Windows SDK 10.0.19041):

```
MSBuild motu-m2-cal.sln -p:Configuration=Release -p:Platform=x64 -t:Restore
MSBuild motu-m2-cal.sln -p:Configuration=Release -p:Platform=x64
```

**GUI uruchamia się przez F5 z Visual Studio.** Ręczna rejestracja zbudowanego
układu (`Add-AppxPackage -Register bin\x64\Debug\AppxManifest.xml`) instaluje
pakiet poprawnie — kontener powstaje, zależności frameworkowe się rozwiązują —
ale aplikacja ginie przy starcie z `FileNotFoundException: System.Private.CoreLib`,
zanim ruszy jakikolwiek kod zarządzany. Dotyczy to zarówno Debug, jak i Release.
Deploy z Visual Studio tego problemu nie ma.

Rdzeń i CLI budują się także zwykłym `dotnet` — bez Visual Studio i poza Windows:

```
dotnet build src/M2Cal.Cli/M2Cal.Cli.csproj
dotnet run --project src/M2Cal.Cli -- selftest
```

Konfiguracja Release projektu UWP idzie przez .NET Native. Modele pliku
kalibracyjnego są serializowane refleksyjnie, więc muszą być zachowane przez
dyrektywy w `src/M2Cal.Uwp/Properties/Default.rd.xml` — bez nich zapis kalibracji
działa w Debug i wywraca się dopiero w Release.

Komendy audio CLI wymagają Windows (WASAPI, tryb współdzielony). `selftest`
działa wszędzie.

## Pakiet instalacyjny

Wydanie powstaje na GitHubie z **taga wersji** — numer wersji trafia do manifestu
pakietu, więc musi być świadomy, a nie efektem ubocznym pusha:

```powershell
.\tools\Bump-Version.ps1        # podnosi rewizję w manifescie i AssemblyInfo
git commit -am "Wersja 1.1.0.2"
git tag v1.1.0.2
git push && git push origin v1.1.0.2
```

Numer podnosi się przy **każdym** wydaniu. Windows odmawia instalacji pakietu
o numerze nie wyższym od już zainstalowanego — pominięcie tego kroku daje na
stanowisku aktualizację, która po cichu nie robi nic.

Workflow `.github/workflows/release.yml` uruchamia selftest, buduje konfigurację
Release x64, podpisuje pakiet i tworzy wydanie z plikami `.msix`, `.cer` oraz
instrukcją `INSTALACJA.md`. Selftest jest bramką — pakiet nie powstanie, jeśli
przeliczenia nie przejdą testów.

Przygotowanie jednorazowe:

```powershell
.\tools\New-PackageCertificate.ps1
```

Skrypt bierze podmiot certyfikatu z `Package.appxmanifest`, żeby nie dało się ich
rozjechać, i wypisuje, co wstawić do sekretów repozytorium
(`SIGNING_CERTIFICATE_BASE64`, `SIGNING_CERTIFICATE_PASSWORD`). Klucz prywatny
zostaje w `artifacts/`, który jest wykluczony z repozytorium.

Pakiet jest podpisany **certyfikatem własnym**, więc na każdym stanowisku trzeba
raz zainstalować część jawną certyfikatu jako zaufaną — szczegóły w
[`docs/INSTALACJA.md`](docs/INSTALACJA.md). Bez tego instalacja kończy się błędem
`0x800B0109`.

## Integracja z aplikacją UWP

1. **Dodaj referencję do `M2Cal.Core`** (netstandard2.0, jedyna zależność:
   System.Text.Json). Modele pliku kalibracyjnego to zwykłe POCO — w razie
   problemów z serializatorem w .NET Native można je czytać dowolnym parserem JSON.
2. **Generuj bodźce przez `ToneSynthesizer`** — ten sam kod, który był wzorcowany.
3. **Przed badaniem wywołaj `DeviceGate.Check(...)`** z aktualnym fingerprintem
   urządzenia: zgodność endpointu, sample rate, głośności, świeżość kalibracji
   (domyślnie ≤365 dni) i pozytywny `verify`. Dopiero wynik `Allowed == true`
   dopuszcza do badania.
4. **Poziom bodźca w badaniu**: `cal.RequiredDbFs(freq, kanał, HL, retspl)`.
5. Wymiana danych narzędzie↔aplikacja odbywa się **plikiem** — celowo. UWP
   domyślnie nie połączy się z lokalnym socketem (network isolation); gdyby
   narzędzie miało być wywoływane z aplikacji na żywo, właściwy wzorzec to proces
   towarzyszący full-trust (`runFullTrust` + `FullTrustProcessLauncher`,
   komunikacja przez AppService), nie localhost.

Uwaga o torze: GUI gra przez `AudioGraph`, czyli tym samym API, którym gra
aplikacja docelowa — wzorcowanie obejmuje tę samą drogę sygnału. CLI gra przez
WASAPI w trybie współdzielonym; oba tory przechodzą przez ten sam mikser Windows
przy tych samych ustawieniach, ale to nie zwalnia z kontroli: końcowe `verify`
(a docelowo pomiar kontrolny bodźców odtwarzanych przez samą aplikację) jest
obowiązkowym domknięciem pętli.

## Ograniczenia i bezpieczeństwo — przeczytaj przed użyciem

- `examples/retspl.tdh39.example.json` zawiera **wartości poglądowe**. Przed
  badaniami trzeba je zastąpić danymi z właściwej części ISO 389 dla faktycznie
  używanej słuchawki (RETSPL zależy od modelu przetwornika i sprzęgacza).
- **Analogowe pokrętła M2 (słuchawkowe i Monitor) są niewidoczne programowo.**
  `DeviceGate` pilnuje tylko stanu software'owego; pozycję pokręteł utrwala
  procedura (oznaczenie pozycji, zdjęcie, codzienna kontrola) —
  patrz `docs/PROCEDURA_WZORCOWANIA.md`.
- Zmiana słuchawek, portu USB, sterownika lub ustawień dźwięku Windows
  **unieważnia kalibrację**.
- To narzędzie pomocnicze, a nie certyfikowany audiometr w rozumieniu
  IEC 60645-1; użycie w badaniach ludzi wymaga nadzoru merytorycznego
  (audiolog/metrolog) i walidacji całego stanowiska.
