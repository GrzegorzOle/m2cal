# Instrukcja operatora

Wzorcowanie toru DAC MOTU M2. Dokument opisuje **każde pole formularza** oraz
sposób wpisywania wartości.

> Narzędzie jest pomocnicze i nie jest certyfikowanym audiometrem w rozumieniu
> IEC 60645-1. Użycie w badaniach ludzi wymaga nadzoru merytorycznego
> (audiolog / metrolog) i walidacji całego stanowiska.

## Zasady wpisywania wartości

| Zasada | Wyjaśnienie |
| --- | --- |
| Separator dziesiętny | Przecinek i kropka są równoważne: `74,3` i `74.3` znaczą to samo. |
| Bez jednostek w polu | Wpisz `74,3`, nie `74,3 dB`. Jednostka jest w etykiecie pola. |
| Bez zaokrąglania | Wpisz tyle miejsc, ile pokazuje miernik. Zaokrąglenie w tę czy w tamtą stronę to nawet 0,5 dB błędu w kalibracji. |
| Puste pole ≠ zero | Pole zostawione puste znaczy „nie zmierzono". Nigdy nie wpisuj `0` zamiast braku danej. |
| Wartości norm | Wpisujesz je **z normą przed sobą**. Narzędzie ich nie zna i nie podpowiada — celowo. |

Formularz w lewej kolumnie jest **dłuższy niż ekran**. Pionowy pasek przewijania
przy jego prawej krawędzi jest widoczny zawsze, także gdy nie ma czego przewijać —
przypomina, że poniżej są dalsze pola.

---

## Przed sesją

1. Ustaw **głośność endpointu Windows na 100 %**. Cała regulacja poziomu odbywa
   się cyfrowo, w dBFS. Zmiana głośności systemowej unieważnia całą mapę.
2. Ustaw analogowe pokrętła M2 (słuchawkowe i Monitor) w oznaczonej pozycji.
   **Są programowo niewidoczne** — żadna aplikacja tego nie sprawdzi. Pilnuje tego
   wyłącznie procedura: oznaczenie pozycji, zdjęcie, kontrola przed każdą sesją.
3. Sprawdź tor pomiarowy kalibratorem akustycznym i **zapisz odczyt**.
4. Osadź przetwornik na sprzęgaczu.

---

## TOR AUDIO (góra okna)

| Pole | Co wpisać |
| --- | --- |
| Lista urządzeń | Wybierz MOTU M2. Jest oznaczone gwiazdką i wybierane samo, jeśli zostanie wykryte. |
| **Otwórz tor** | Kliknij przed pierwszym pomiarem. Do tego czasu **Graj jest nieaktywne**. |

Pod listą wyświetla się częstotliwość próbkowania, rozdzielczość i liczba kanałów.
Te wartości trafiają do pliku jako odcisk toru.

## BODZIEC

| Pole | Jednostka | Co wpisać |
| --- | --- | --- |
| **Częstotliwość** | Hz | Częstotliwość nośna. Dowolna wartość dodatnia. |
| **Poziom sygnału** | dBFS | Poziom podawany na wyjście. Zawsze ujemny; `0` to pełna skala. Przyciski `−5 / −1 / +1 / +5` zmieniają go krokowo, zakres −120…0. |
| **Kanał** | — | `L` albo `P`. Drugi kanał jest cyfrową ciszą. |
| **pulsowany** | — | Bodziec przerywany — taki, jakiego używa test przesiewowy. |
| **wobbulowany** | — | Modulacja częstotliwości. Stosowana w polu swobodnym, **nie** przy wzorcowaniu w słuchawkach. |

**Graj / Stop** — ton brzmi nieprzerwanie aż do zatrzymania, żeby miernik zdążył
się ustabilizować.

Poziom wolno zmieniać w trakcie grania — pole i generator pozostają zgodne.
Zmiana **częstotliwości, kanału lub rodzaju bodźca zatrzymuje ton**: odczyt
z miernika ma odpowiadać dokładnie temu bodźcowi, który za chwilę zapiszesz.

## POMIAR

| Pole | Jednostka | Co wpisać |
| --- | --- | --- |
| **Zmierzony poziom** | dB SPL | Odczyt z miernika klasy 1. |
| Notatka | — | Nieobowiązkowa, np. „powtórka po poprawieniu osadzenia". |

Pod polem wyświetla się **czułość toru** — poziom, jaki dałby sinus pełnej skali.
To wartość pochodna (`zmierzone − poziom bodźca`), licząca się na bieżąco.

**Dodaj pomiar do mapy →** dopisuje wiersz do tabeli po prawej. Jeśli przycisk jest
szary, linijka nad nim mówi, czego brakuje.

Tę samą częstotliwość możesz zmierzyć przy kilku poziomach — rozrzut wynikającej
z nich czułości jest miarą nieliniowości toru. Rozrzut powyżej 2 dB jest
sygnalizowany i zwykle oznacza przesuniętą słuchawkę, a nie wadę toru.

## SESJA

| Pole | Co wpisać |
| --- | --- |
| **Operator** | Imię i nazwisko osoby prowadzącej wzorcowanie. Wymagane. |

## PRZETWORNIK

| Pole | Co wpisać |
| --- | --- |
| **Model** | Np. `TDH-39`. Determinuje właściwą tabelę RETSPL. Wymagane. |
| **Numer seryjny** | Wymagany — bez niego pomiar nie jest przypisany do egzemplarza. |
| **Poduszki** | `supraauralne`, `dookołouszne` albo `douszne`. |

## MIERNIK POZIOMU DŹWIĘKU

| Pole | Co wpisać |
| --- | --- |
| **Producent, Model, Numer seryjny** | Model i numer seryjny są wymagane. |
| **Norma i klasa** | Np. `IEC 61672-1 klasa 1` — przepisz z tabliczki lub świadectwa. |
| **Świadectwo — numer i data** | Wymagane. Bez świadectwa pomiar nie jest spójny pomiarowo, a wynik nie do obrony w publikacji. |
| **Ważenie częstotliwościowe** | `Z`, `C` albo `A` — nastawa miernika w trakcie pomiaru. |
| **Ważenie czasowe** | `F` albo `S`. |
| **Odczytywana wielkość** | `SPL`, `Leq` albo `SPLmax` — to, co faktycznie odczytujesz. |

Ważenie i wielkość zapisz **takie, jakie były nastawione**, a nie takie, jakie
powinny być. Plik ma opisywać przebieg pomiaru, nie zamiar.

## MIKROFON

Model i numer seryjny. Zalecane.

## SPRZĘGACZ

| Pole | Co wpisać |
| --- | --- |
| **Model, Numer seryjny** | Wymagane. |
| **Norma sprzęgacza** | Np. `IEC 60318-3`. Wymagane. **Musi odpowiadać tabeli RETSPL** — RETSPL zależy od pary przetwornik + sprzęgacz, a nie od samego przetwornika. |

## KALIBRATOR AKUSTYCZNY

| Pole | Jednostka | Co wpisać |
| --- | --- | --- |
| **Model, Numer seryjny** | — | Identyfikacja kalibratora. |
| **Poziom** | dB SPL | Poziom odniesienia z jego świadectwa. |
| **Częstotliwość** | Hz | Częstotliwość odniesienia. |
| **Odczyt przed sesją** | dB SPL | Wymagany. |
| **Odczyt po sesji** | dB SPL | Uzupełnij po zakończeniu pomiarów. |

Różnica obu odczytów to dryf toru pomiarowego. **Przekroczenie 0,5 dB blokuje
dopuszczenie** — skoro tor przesunął się w trakcie sesji, nie wiadomo, któremu
odczytowi wierzyć, i sesję trzeba powtórzyć.

## WARUNKI

Hałas tła w dB(A), temperatura w °C, wilgotność w %, pomieszczenie. Hałas tła
zalecany — bez niego nie wykażesz, że pomiar nie był zakłócony.

## ŹRÓDŁA WARTOŚCI NORMATYWNYCH

Każde pole to **norma wraz z wydaniem**, np. `ISO 389-1:2017`.

| Pole | Czego dotyczy |
| --- | --- |
| **RETSPL** | Część serii ISO 389 właściwa dla użytej pary przetwornik + sprzęgacz. Wymagane. |
| **Tolerancja poziomu** | Źródło tolerancji przyjętej przy kontroli `verify`. Wymagane. |
| **Czasy bodźca** | Źródło czasów narastania i trwania impulsu. Wymagane. |
| **Miernik** | Norma miernika wraz z klasą. |
| **Sprzęgacz** | Norma sprzęgacza. |
| **Dopuszczalny hałas tła** | Norma określająca dopuszczalny hałas w pomieszczeniu badań. |

To najważniejsza sekcja dla wiarygodności wyniku. Narzędzie **nie zna treści norm
i niczego tu nie podpowiada** — świadomie, żeby żadna niesprawdzona wartość nie
trafiła do publikacji pod pozorem domyślnej. Odpowiadasz za nie Ty.

---

## Po sesji

1. Uzupełnij **odczyt kalibratora po sesji**.
2. **Zapisz plik kalibracyjny** (przycisk w prawej kolumnie).
3. Wykonaj kontrolę `verify` — bez niej bramka odrzuci plik:
   ```
   m2cal verify --cal calibration.m2cal.json --retspl <tabela> --hl 20
   ```

Pasek stanu na dole okna wypisuje wprost, czego brakuje do dopuszczenia.

## Co unieważnia kalibrację

- zmiana słuchawek, portu USB, sterownika lub ustawień dźwięku Windows,
- zmiana głośności endpointu,
- poruszenie analogowych pokręteł M2,
- zmiana wersji syntezy bodźców (`synthesizerVersion` w pliku),
- upływ ważności — domyślnie 365 dni.

## Częste błędy

| Objaw | Przyczyna |
| --- | --- |
| **Graj** jest szare | Nie kliknięto „Otwórz tor". |
| **Dodaj pomiar do mapy** jest szare | Brakuje częstotliwości, poziomu albo odczytu. Linijka nad przyciskiem mówi czego. |
| Bramka odrzuca gotowy plik | Najczęściej brak `verify`, brak potwierdzenia głośności endpointu albo puste pole źródła normy. |
| Rozrzut czułości powyżej 2 dB | Zwykle przesunięta słuchawka między powtórzeniami, rzadziej nieliniowość toru. |
| Wymagany poziom przekracza 0 dBFS | Tor jest za mało czuły dla żądanego dB HL. Nie obcinaj — zgłoś. |
