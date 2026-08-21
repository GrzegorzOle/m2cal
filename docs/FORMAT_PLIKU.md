# Format pliku kalibracyjnego

Dokument opisuje `calibration.m2cal.json` — artefakt przenoszony do aplikacji
docelowej i załączany do dokumentacji badania. Opis ma być na tyle jednoznaczny,
żeby **aplikacja docelowa zapisywała i odczytywała dokładnie te same wartości**.

Wzorzec pliku z kompletem pól generuje samo narzędzie, więc opis nie może rozjechać
się z kodem:

```
m2cal example --out wzorzec.json
```

## Konwencje obowiązujące w całym pliku

| Zasada | Wartość |
| --- | --- |
| Kodowanie | UTF-8 bez BOM |
| Nazwy pól | `camelCase`, odczyt bez rozróżniania wielkości liter |
| Znaczniki czasu | ISO 8601 w **UTC**, sufiks `Z` |
| Separator dziesiętny | kropka (format JSON) |
| Poziom cyfrowy | dBFS, gdzie **sinus pełnej skali = 0 dBFS** (amplituda szczytowa 1,0) |
| Poziom akustyczny | dB SPL, odniesienie 20 µPa |
| Poziom słyszenia | dB HL |
| Brak danej | `null` — nigdy pusty ciąg ani `0` |

Jednostka jest częścią **nazwy pola** (`riseFallMs`, `backgroundNoiseDbA`,
`nominalLevelDbSpl`). Nie ma pól, w których jednostkę trzeba by zgadywać, i nie
wolno wprowadzać takich pól w przyszłości.

## Nagłówek

| Pole | Typ | Wymagane | Znaczenie |
| --- | --- | --- | --- |
| `schemaVersion` | int | tak | Wersja schematu. Obecnie **2**. Plik o innej wersji jest odrzucany. |
| `createdAtUtc` | data | tak | Chwila zapisania mapy. Od niej liczona jest ważność kalibracji. |
| `operator` | tekst | tak | Osoba prowadząca wzorcowanie. |
| `transducer`, `coupler` | tekst | nie | Skrócone nazwy, dla czytelności. Dane wiążące są w `transducerDetails` i `equipment`. |
| `refDbFs` | liczba | tak | Domyślny poziom bodźca odniesienia proponowany w formularzu. **Nie** jest używany w przeliczeniach. |
| `synthesizerVersion` | int | tak | Wersja kodu syntezy bodźca. Niezgodność z wersją w aplikacji blokuje badanie. |
| `notes` | tekst | nie | Notatki operatora. |

## `device` — odcisk toru audio

| Pole | Typ | Znaczenie |
| --- | --- | --- |
| `deviceId` | tekst | Identyfikator endpointu audio. |
| `deviceName` | tekst | Nazwa widoczna dla użytkownika, wyłącznie diagnostycznie. |
| `sampleRate` | int | Hz. |
| `bitDepth` | int | bity. |
| `channelCount` | int | Liczba kanałów. |
| `endpointVolumePercent` | int | Głośność endpointu, 0–100. Wzorcowanie wymaga **100**. Wartość `-1` oznacza brak potwierdzenia i blokuje dopuszczenie. |

Każda rozbieżność między tym odciskiem a stanem faktycznym unieważnia kalibrację.

## `equipment` — tor pomiarowy

| Pole | Typ | Wymagane | Znaczenie |
| --- | --- | --- | --- |
| `soundLevelMeter` | przyrząd | tak | Miernik poziomu dźwięku. |
| `microphone` | przyrząd | zalecane | Mikrofon pomiarowy. |
| `coupler` | przyrząd | tak | Sprzęgacz albo ucho sztuczne. |
| `couplerStandard` | tekst | tak | Norma sprzęgacza. Musi odpowiadać tabeli RETSPL — RETSPL zależy od pary przetwornik + sprzęgacz. |
| `frequencyWeighting` | tekst | tak | `Z`, `C` albo `A`. |
| `timeWeighting` | tekst | zalecane | `F` albo `S`. |
| `measurementMode` | tekst | zalecane | `SPL`, `Leq` albo `SPLmax`. |
| `integrationTimeSeconds` | liczba | nie | Czas uśredniania, jeśli odczytywano `Leq`. |
| `calibratorCheck` | obiekt | tak | Sprawdzenie toru kalibratorem akustycznym. |

Każdy **przyrząd** ma tę samą strukturę: `manufacturer`, `model`, `serialNumber`,
`conformsToStandard`, `calibrationCertificate`, `calibratedOnUtc`,
`calibrationValidUntilUtc`. Przyrząd uznaje się za zidentyfikowany, gdy ma model
i numer seryjny; za spójny pomiarowo — gdy ma numer świadectwa i datę wzorcowania.

### `calibratorCheck`

| Pole | Typ | Znaczenie |
| --- | --- | --- |
| `calibrator` | przyrząd | Kalibrator akustyczny. |
| `nominalLevelDbSpl` | liczba | Poziom odniesienia z jego świadectwa. |
| `nominalFrequencyHz` | liczba | Częstotliwość odniesienia. |
| `readingBeforeSessionDbSpl` | liczba | Odczyt **przed** sesją. Wymagany. |
| `readingAfterSessionDbSpl` | liczba | Odczyt **po** sesji. |

Różnica obu odczytów to dryf toru pomiarowego. Przekroczenie **0,5 dB** blokuje
dopuszczenie: skoro tor pomiarowy przesunął się w trakcie sesji, nie wiadomo,
któremu odczytowi wierzyć.

## `transducerDetails` — przetwornik

`manufacturer`, `model`, `serialNumber`, `cushionType`
(`supraauralne` / `dookołouszne` / `douszne`), `headbandForceNewton` (opcjonalnie).

## `stimulus` — parametry syntezy bodźca

To sekcja, którą **aplikacja docelowa musi odtworzyć co do wartości**.

| Pole | Jednostka | Znaczenie |
| --- | --- | --- |
| `waveform` | — | Przebieg. Obecnie `sinus`. |
| `levelConvention` | — | Zapisana jawnie konwencja poziomu. |
| `riseFallMs` | ms | Czas narastania i opadania obwiedni. |
| `pulseOnMs`, `pulseOffMs` | ms | Czasy trwania impulsu i przerwy w bodźcu pulsowanym. |
| `pulsed` | — | Czy bodziec był pulsowany. |
| `warble` | — | Czy bodziec był wobbulowany. |
| `warbleDepthPercent` | % | Głębokość modulacji częstotliwości. |
| `warbleRateHz` | Hz | Częstotliwość modulacji. |
| `envelopeShape` | — | Kształt obwiedni. Obecnie `podniesiony cosinus`. |
| `sampleRate` | Hz | Częstotliwość próbkowania syntezy. |
| `synthesizerVersion` | — | Wersja kodu syntezy. |
| `timingSource` | — | **Norma wraz z wydaniem**, z której dobrano czasy. Wpisuje operator. |

Wartości czasowe są nastawami przyjętymi w tym narzędziu, a **nie** cytatem
z normy. Narzędzie nie rozstrzyga, czy są zgodne z normą — nie zna jej treści.
Za to rozstrzygnięcie odpowiada operator i dokumentuje je w `timingSource`.

## `ambient` — warunki otoczenia

`backgroundNoiseDbA`, `temperatureCelsius`, `relativeHumidityPercent`,
`atmosphericPressureHpa`, `location`.

## `standards` — źródła wartości normatywnych

| Pole | Czego dotyczy |
| --- | --- |
| `retspl` | Część serii ISO 389 właściwa dla użytej pary przetwornik + sprzęgacz. |
| `levelTolerance` | Źródło przyjętej tolerancji przy kontroli `verify`. |
| `stimulusTiming` | Źródło czasów bodźca. |
| `soundLevelMeter` | Norma miernika wraz z klasą. |
| `coupler` | Norma sprzęgacza. |
| `ambientNoise` | Dopuszczalny hałas tła w pomieszczeniu badań. |

Każde pole to **norma wraz z wydaniem**, np. `ISO 389-1:2017`. Narzędzie nie zna
treści norm i nie podstawia tu żadnych wartości. Brak `retspl` albo
`levelTolerance` blokuje dopuszczenie.

## `points` — mapa pomiarów

Surowe obserwacje. Jeden wpis to jeden pomiar.

| Pole | Jednostka | Znaczenie |
| --- | --- | --- |
| `frequencyHz` | Hz | Częstotliwość bodźca. |
| `ear` | — | `L` albo `P`. |
| `stimulusDbFs` | dBFS | Poziom podany na wyjście. |
| `measuredSpl` | dB SPL | Odczyt z miernika. |
| `measuredAtUtc` | data | Chwila pomiaru. |
| `note` | — | Notatka operatora. |
| `splAtFullScale` | dB SPL | **Wartość pochodna**, patrz niżej. |

`splAtFullScale` jest zapisywana dla czytelności, ale przy odczycie **jest
ignorowana i liczona od nowa**:

```
splAtFullScale = measuredSpl − stimulusDbFs
```

Nie traktuj jej jako danej wejściowej. Gdyby plik został ręcznie zmieniony
niespójnie, wiążące są `measuredSpl` i `stimulusDbFs`.

Ta sama para (`frequencyHz`, `ear`) może wystąpić wielokrotnie, przy różnych
poziomach — pozwala to sprawdzić liniowość toru. Czułość liczy się wtedy jako
średnią, a rozrzut powyżej 2 dB jest sygnalizowany.

## `verify` — kontrola

| Pole | Znaczenie |
| --- | --- |
| `performedAtUtc` | Chwila kontroli. |
| `passed` | Wynik. Bez `true` bramka odrzuca plik. |
| `maxDeviationDb` | Największe odchylenie bezwzględne. |
| `toleranceDb` | Przyjęta tolerancja. Jej źródło jest w `standards.levelTolerance`. |
| `retsplSource` | Tabela RETSPL użyta przy kontroli. |
| `points` | Poszczególne punkty: `frequencyHz`, `ear`, `hearingLevelDb`, `expectedSpl`, `measuredSpl`. |

## Przeliczenia, które musi powtórzyć aplikacja docelowa

```
czułość toru = measuredSpl − stimulusDbFs        (uśredniona po punktach danej pary)
cel dB SPL   = RETSPL(f) + HL
dBFS         = cel dB SPL − czułość toru
```

Dwie zasady, których nie wolno złamać:

1. **Bez interpolacji.** Poziom wyznacza się wyłącznie dla częstotliwości obecnych
   w mapie. Ekstrapolacja poza punkty pomiarowe nie jest wzorcowaniem.
2. **Bez regulacji poza dBFS.** Głośność endpointu pozostaje na 100 %, a cała
   zmiana poziomu odbywa się cyfrowo. Każde inne skalowanie unieważnia mapę.

Wynik powyżej `0 dBFS` oznacza, że tor jest za mało czuły dla żądanego poziomu —
taki bodziec należy odrzucić, a nie obciąć.

## Warunki dopuszczenia do badania

Bramka (`DeviceGate`) przepuszcza plik tylko wtedy, gdy spełnione są **wszystkie**
warunki: zgodna wersja schematu i syntezy, zgodny odcisk urządzenia, głośność
endpointu 100 %, kalibracja nieprzeterminowana, `verify.passed = true`, komplet
danych o torze pomiarowym i źródłach normatywnych, dryf kalibratora poniżej
0,5 dB oraz tabela RETSPL nieoznaczona jako przykładowa.
