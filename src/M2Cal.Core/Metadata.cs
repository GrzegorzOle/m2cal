using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace M2Cal.Core
{
    /// <summary>
    /// Identyfikacja przyrządu pomiarowego. Bez modelu i numeru seryjnego pomiar nie jest
    /// odtwarzalny, a bez świadectwa wzorcowania nie jest spójny pomiarowo — dlatego oba
    /// komplety danych zapisuje się w pliku, a nie w notatniku operatora.
    /// </summary>
    public sealed class InstrumentInfo
    {
        public string Manufacturer { get; set; }

        public string Model { get; set; }

        public string SerialNumber { get; set; }

        /// <summary>Norma i klasa, którym przyrząd odpowiada, np. „IEC 61672-1 klasa 1”.</summary>
        public string ConformsToStandard { get; set; }

        /// <summary>Numer świadectwa wzorcowania przyrządu.</summary>
        public string CalibrationCertificate { get; set; }

        public DateTime? CalibratedOnUtc { get; set; }

        public DateTime? CalibrationValidUntilUtc { get; set; }

        [JsonIgnore]
        public bool IsIdentified =>
            !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(SerialNumber);

        [JsonIgnore]
        public bool HasTraceability =>
            !string.IsNullOrWhiteSpace(CalibrationCertificate) && CalibratedOnUtc.HasValue;
    }

    /// <summary>
    /// Sprawdzenie toru pomiarowego kalibratorem akustycznym. Wykonuje się je przed sesją
    /// i po niej — różnica odczytów jest miarą dryfu toru w trakcie wzorcowania i decyduje,
    /// czy wyniki sesji wolno uznać.
    /// </summary>
    public sealed class AcousticCalibratorCheck
    {
        public InstrumentInfo Calibrator { get; set; }

        /// <summary>Poziom odniesienia kalibratora w dB SPL — wartość z jego świadectwa.</summary>
        public double? NominalLevelDbSpl { get; set; }

        /// <summary>Częstotliwość odniesienia kalibratora w Hz.</summary>
        public double? NominalFrequencyHz { get; set; }

        public double? ReadingBeforeSessionDbSpl { get; set; }

        public double? ReadingAfterSessionDbSpl { get; set; }

        /// <summary>Dryf toru pomiarowego w trakcie sesji, w dB.</summary>
        [JsonIgnore]
        public double? DriftDb =>
            ReadingBeforeSessionDbSpl.HasValue && ReadingAfterSessionDbSpl.HasValue
                ? ReadingAfterSessionDbSpl.Value - ReadingBeforeSessionDbSpl.Value
                : (double?)null;
    }

    /// <summary>Tor pomiarowy: czym i w jakich nastawach mierzono poziom akustyczny.</summary>
    public sealed class MeasurementChain
    {
        public InstrumentInfo SoundLevelMeter { get; set; }

        public InstrumentInfo Microphone { get; set; }

        /// <summary>Sprzęgacz albo ucho sztuczne, na którym osadzono przetwornik.</summary>
        public InstrumentInfo Coupler { get; set; }

        /// <summary>
        /// Norma sprzęgacza, np. „IEC 60318-1” albo „IEC 60318-3”. Musi odpowiadać tabeli
        /// RETSPL użytej w przeliczeniach — RETSPL zależy od pary przetwornik + sprzęgacz.
        /// </summary>
        public string CouplerStandard { get; set; }

        public AcousticCalibratorCheck CalibratorCheck { get; set; }

        /// <summary>Ważenie częstotliwościowe miernika: „Z”, „C” albo „A”.</summary>
        public string FrequencyWeighting { get; set; }

        /// <summary>Ważenie czasowe miernika: „F” (fast) albo „S” (slow).</summary>
        public string TimeWeighting { get; set; }

        /// <summary>Wielkość odczytywana: „SPL”, „Leq” albo „SPLmax”.</summary>
        public string MeasurementMode { get; set; }

        /// <summary>Czas uśredniania, jeśli odczytywano Leq.</summary>
        public double? IntegrationTimeSeconds { get; set; }
    }

    /// <summary>Przetwornik, na który podawany jest bodziec.</summary>
    public sealed class TransducerInfo
    {
        public string Manufacturer { get; set; }

        public string Model { get; set; }

        public string SerialNumber { get; set; }

        /// <summary>Rodzaj poduszek: „supraauralne”, „dookołouszne”, „douszne”.</summary>
        public string CushionType { get; set; }

        /// <summary>Siła docisku pałąka, jeśli mierzona.</summary>
        public double? HeadbandForceNewton { get; set; }

        [JsonIgnore]
        public bool IsIdentified =>
            !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(SerialNumber);
    }

    /// <summary>
    /// Komplet parametrów syntezy bodźca. Zapisany w pliku po to, żeby aplikacja docelowa
    /// odtworzyła bodziec identycznie, a czytelnik publikacji mógł sprawdzić, co dokładnie
    /// podawano na przetwornik. Wartości pochodzą z <see cref="ToneSynthesizer"/>.
    /// </summary>
    public sealed class StimulusSettings
    {
        public string Waveform { get; set; } = "sinus";

        /// <summary>Konwencja poziomu — zapisana jawnie, bo od niej zależy sens każdego dBFS.</summary>
        public string LevelConvention { get; set; } = "sinus pelnej skali = 0 dBFS (amplituda szczytowa 1,0)";

        public double RiseFallMs { get; set; }

        public double PulseOnMs { get; set; }

        public double PulseOffMs { get; set; }

        public bool Pulsed { get; set; }

        public bool Warble { get; set; }

        public double WarbleDepthPercent { get; set; }

        public double WarbleRateHz { get; set; }

        public string EnvelopeShape { get; set; } = "podniesiony cosinus";

        public int SampleRate { get; set; }

        /// <summary>
        /// Wersja kodu syntezy. Zmiana któregokolwiek z powyższych parametrów musi ją podnieść,
        /// bo plik kalibracyjny przestaje wtedy opisywać bodziec, który faktycznie zabrzmi.
        /// </summary>
        public int SynthesizerVersion { get; set; }

        /// <summary>
        /// Skąd wzięto czasy narastania, opadania i trwania impulsu — norma wraz z wydaniem.
        /// Narzędzie nie wpisuje tu nic samo: to dane wejściowe, za które odpowiada operator.
        /// </summary>
        public string TimingSource { get; set; }

        /// <summary>Odczytuje aktualne nastawy z syntezatora, bez przepisywania ich ręcznie.</summary>
        public static StimulusSettings FromSynthesizer(ToneSynthesizer synth)
        {
            if (synth == null) throw new ArgumentNullException(nameof(synth));

            return new StimulusSettings
            {
                RiseFallMs = ToneSynthesizer.RampSeconds * 1000.0,
                PulseOnMs = ToneSynthesizer.PulseOnSeconds * 1000.0,
                PulseOffMs = ToneSynthesizer.PulseOffSeconds * 1000.0,
                WarbleDepthPercent = ToneSynthesizer.WarbleDepth * 100.0,
                WarbleRateHz = ToneSynthesizer.WarbleRateHz,
                Pulsed = synth.Pulsed,
                Warble = synth.Warble,
                SampleRate = synth.SampleRate,
                SynthesizerVersion = ToneSynthesizer.Version
            };
        }
    }

    /// <summary>Warunki otoczenia w trakcie sesji wzorcowania.</summary>
    public sealed class AmbientConditions
    {
        /// <summary>Poziom hałasu tła w pomieszczeniu, dB(A).</summary>
        public double? BackgroundNoiseDbA { get; set; }

        public double? TemperatureCelsius { get; set; }

        public double? RelativeHumidityPercent { get; set; }

        public double? AtmosphericPressureHpa { get; set; }

        /// <summary>Pomieszczenie albo stanowisko, na którym prowadzono wzorcowanie.</summary>
        public string Location { get; set; }
    }

    /// <summary>
    /// Źródła wartości normatywnych. Każda wartość, która nie wynika z pomiaru ani z kodu,
    /// musi mieć tu podaną normę wraz z wydaniem. Narzędzie żadnej z nich nie zna i nie
    /// zgaduje — wpisuje je operator, mając normę przed sobą.
    /// </summary>
    public sealed class StandardsReferences
    {
        /// <summary>Część serii ISO 389 właściwa dla użytej pary przetwornik + sprzęgacz.</summary>
        public string Retspl { get; set; }

        /// <summary>Źródło przyjętej tolerancji poziomu przy kontroli verify.</summary>
        public string LevelTolerance { get; set; }

        /// <summary>Źródło czasów narastania, opadania i trwania bodźca.</summary>
        public string StimulusTiming { get; set; }

        /// <summary>Norma miernika poziomu dźwięku wraz z klasą.</summary>
        public string SoundLevelMeter { get; set; }

        /// <summary>Norma sprzęgacza albo ucha sztucznego.</summary>
        public string Coupler { get; set; }

        /// <summary>Dopuszczalny poziom hałasu tła w pomieszczeniu badań.</summary>
        public string AmbientNoise { get; set; }
    }

    /// <summary>Braki w udokumentowaniu sesji, z podziałem na blokujące i wymagające uzupełnienia.</summary>
    public sealed class ProvenanceReport
    {
        public List<string> Missing { get; } = new List<string>();

        public List<string> Incomplete { get; } = new List<string>();

        [JsonIgnore]
        public bool IsComplete => Missing.Count == 0;
    }
}
