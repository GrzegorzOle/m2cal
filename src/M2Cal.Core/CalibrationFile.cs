using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace M2Cal.Core
{
    /// <summary>
    /// Pojedynczy punkt mapy wzorcowania: przy jakiej częstotliwości i jakim poziomie cyfrowym
    /// operator zmierzył jaki poziom akustyczny. To jest surowa obserwacja — wszystkie
    /// przeliczenia wyprowadzane są z niej, nic nie jest zapisywane w postaci przetworzonej.
    /// </summary>
    public sealed class CalibrationPoint
    {
        public double FrequencyHz { get; set; }

        /// <summary>Kanał, na którym grał bodziec: "L" albo "P".</summary>
        public string Ear { get; set; } = "L";

        /// <summary>Poziom bodźca podany na wyjście, w dBFS (sinus pełnej skali = 0 dBFS).</summary>
        public double StimulusDbFs { get; set; }

        /// <summary>Poziom zmierzony miernikiem klasy 1 na sprzęgaczu, w dB SPL.</summary>
        public double MeasuredSpl { get; set; }

        /// <summary>Znacznik czasu pomiaru (UTC), do śledzenia przebiegu sesji wzorcowania.</summary>
        public DateTime MeasuredAtUtc { get; set; }

        /// <summary>Nieobowiązkowa notatka operatora (np. „powtórka po poprawieniu osadzenia").</summary>
        public string Note { get; set; }

        /// <summary>
        /// Czułość toru wynikająca z tego punktu: poziom akustyczny, jaki dałby sinus pełnej skali.
        /// Przy liniowym torze wartość ta nie zależy od <see cref="StimulusDbFs"/>, więc rozrzut
        /// między punktami tej samej częstotliwości jest miarą nieliniowości.
        /// </summary>
        public double SplAtFullScale => MeasuredSpl - StimulusDbFs;

        [JsonIgnore]
        public Ear EarValue
        {
            get
            {
                EarExtensions.TryParse(Ear, out Ear parsed);
                return parsed;
            }
        }
    }

    /// <summary>Odcisk konfiguracji toru w chwili wzorcowania. Każda rozbieżność unieważnia kalibrację.</summary>
    public sealed class DeviceFingerprint
    {
        /// <summary>Identyfikator endpointu audio (WASAPI / AudioGraph DeviceInformation.Id).</summary>
        public string DeviceId { get; set; }

        /// <summary>Nazwa urządzenia widoczna dla użytkownika, tylko do diagnostyki.</summary>
        public string DeviceName { get; set; }

        public int SampleRate { get; set; }

        public int BitDepth { get; set; }

        public int ChannelCount { get; set; }

        /// <summary>Głośność endpointu 0–100. Wzorcowanie wymaga 100.</summary>
        public int EndpointVolumePercent { get; set; }

        public bool Matches(DeviceFingerprint other, out string reason)
        {
            reason = null;
            if (other == null) { reason = "brak odcisku urządzenia"; return false; }

            if (!string.Equals(DeviceId, other.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"inny endpoint audio (kalibracja: {DeviceName ?? DeviceId}, teraz: {other.DeviceName ?? other.DeviceId})";
                return false;
            }
            if (SampleRate != other.SampleRate)
            {
                reason = $"inna częstotliwość próbkowania (kalibracja: {SampleRate} Hz, teraz: {other.SampleRate} Hz)";
                return false;
            }
            if (ChannelCount != other.ChannelCount)
            {
                reason = $"inna liczba kanałów (kalibracja: {ChannelCount}, teraz: {other.ChannelCount})";
                return false;
            }
            if (EndpointVolumePercent != other.EndpointVolumePercent)
            {
                reason = $"inna głośność endpointu (kalibracja: {EndpointVolumePercent} %, teraz: {other.EndpointVolumePercent} %)";
                return false;
            }
            return true;
        }
    }

    /// <summary>Wynik kontroli <c>verify</c> — odtworzenia poziomów w dB HL i sprawdzenia ich miernikiem.</summary>
    public sealed class VerifyResult
    {
        public DateTime PerformedAtUtc { get; set; }

        public bool Passed { get; set; }

        /// <summary>Największe odchylenie od wartości zadanej, w dB.</summary>
        public double MaxDeviationDb { get; set; }

        public double ToleranceDb { get; set; }

        public string RetsplSource { get; set; }

        public List<VerifyPoint> Points { get; set; } = new List<VerifyPoint>();
    }

    public sealed class VerifyPoint
    {
        public double FrequencyHz { get; set; }
        public string Ear { get; set; }
        public double HearingLevelDb { get; set; }
        public double ExpectedSpl { get; set; }
        public double MeasuredSpl { get; set; }
        public double DeviationDb => MeasuredSpl - ExpectedSpl;
    }

    /// <summary>
    /// Plik kalibracyjny — artefakt przenoszony do aplikacji docelowej. Zwykłe POCO, bez
    /// konstrukcji zależnych od refleksji, żeby dało się go odczytać dowolnym parserem JSON.
    /// </summary>
    public sealed class CalibrationFile
    {
        /// <summary>
        /// Wersja 2 wprowadza obowiązkowe udokumentowanie toru pomiarowego i źródeł wartości
        /// normatywnych. Pliki w wersji 1 są odrzucane celowo: nie zawierają danych, bez
        /// których wyniku nie da się obronić ani odtworzyć.
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>Domyślny okres ważności kalibracji.</summary>
        public const int DefaultMaxAgeDays = 365;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAtUtc { get; set; }

        /// <summary>Osoba prowadząca wzorcowanie — wymagane przez procedurę.</summary>
        public string Operator { get; set; }

        /// <summary>Model przetwornika, np. „TDH-39". Determinuje właściwą tabelę RETSPL.</summary>
        public string Transducer { get; set; }

        /// <summary>Sprzęgacz użyty przy pomiarze, np. „NBS 9A" / „IEC 60318-1".</summary>
        public string Coupler { get; set; }

        /// <summary>Domyślny poziom bodźca odniesienia proponowany w GUI, w dBFS.</summary>
        public double RefDbFs { get; set; } = -20.0;

        /// <summary>
        /// Wersja kodu syntezy bodźców. Zmiana <see cref="ToneSynthesizer"/> musi podnieść tę
        /// wartość — inaczej aplikacja docelowa użyłaby kalibracji opisującej inny bodziec.
        /// </summary>
        public int SynthesizerVersion { get; set; } = ToneSynthesizer.Version;

        public DeviceFingerprint Device { get; set; }

        /// <summary>Czym i w jakich nastawach mierzono poziom akustyczny.</summary>
        public MeasurementChain Equipment { get; set; }

        /// <summary>Przetwornik, na który podawano bodziec.</summary>
        public TransducerInfo TransducerDetails { get; set; }

        /// <summary>Komplet parametrów syntezy — pozwala aplikacji docelowej odtworzyć bodziec.</summary>
        public StimulusSettings Stimulus { get; set; }

        /// <summary>Warunki otoczenia w trakcie sesji.</summary>
        public AmbientConditions Ambient { get; set; }

        /// <summary>Normy, z których pochodzą wartości nie wynikające z pomiaru ani z kodu.</summary>
        public StandardsReferences Standards { get; set; }

        /// <summary>Mapa pomiarów: częstotliwość → poziom cyfrowy → zmierzony poziom akustyczny.</summary>
        public List<CalibrationPoint> Points { get; set; } = new List<CalibrationPoint>();

        public VerifyResult Verify { get; set; }

        /// <summary>Notatki operatora, np. oznaczenie pozycji analogowych pokręteł M2.</summary>
        public string Notes { get; set; }

        /// <summary>Częstotliwości obecne w mapie, rosnąco.</summary>
        [JsonIgnore]
        public IEnumerable<double> Frequencies =>
            Points.Select(p => p.FrequencyHz).Distinct().OrderBy(f => f);

        /// <summary>
        /// Czułość toru dla danej częstotliwości i ucha: poziom akustyczny sinusa pełnej skali.
        /// Uśredniona po wszystkich punktach mapy dla tej pary — rozrzut zwracany osobno.
        /// </summary>
        public bool TryGetSensitivity(double frequencyHz, Ear ear, out double splAtFullScale, out double spreadDb)
        {
            splAtFullScale = 0.0;
            spreadDb = 0.0;

            var matching = Points
                .Where(p => Math.Abs(p.FrequencyHz - frequencyHz) < 0.5 && p.EarValue == ear)
                .Select(p => p.SplAtFullScale)
                .ToList();

            if (matching.Count == 0) return false;

            splAtFullScale = matching.Average();
            spreadDb = matching.Max() - matching.Min();
            return true;
        }

        /// <summary>
        /// Poziom cyfrowy bodźca (dBFS) potrzebny, by uzyskać zadany poziom słyszenia (dB HL).
        ///
        ///   cel dB SPL = RETSPL(f) + HL
        ///   dBFS       = cel dB SPL − SPL(sinus pełnej skali)
        ///
        /// Zwraca false, jeśli mapa nie zawiera tej częstotliwości i ucha — świadomie bez
        /// interpolacji, bo ekstrapolacja poza punkty pomiarowe nie jest wzorcowaniem.
        /// </summary>
        public bool TryGetRequiredDbFs(double frequencyHz, Ear ear, double hearingLevelDb,
                                       RetsplTable retspl, out double dbFs, out string error)
        {
            dbFs = 0.0;
            error = null;

            if (retspl == null) { error = "brak tabeli RETSPL"; return false; }

            if (!retspl.TryGetRetspl(frequencyHz, out double retsplDb))
            {
                error = $"tabela RETSPL nie zawiera {frequencyHz:0} Hz";
                return false;
            }

            if (!TryGetSensitivity(frequencyHz, ear, out double splAtFullScale, out _))
            {
                error = $"mapa kalibracyjna nie zawiera {frequencyHz:0} Hz dla kanału {ear.ToCode()}";
                return false;
            }

            double targetSpl = retsplDb + hearingLevelDb;
            dbFs = targetSpl - splAtFullScale;

            if (dbFs > 0.0)
            {
                error = $"wymagany poziom {dbFs:0.0} dBFS przekracza pełną skalę — tor jest za mało czuły";
                return false;
            }

            return true;
        }

        /// <summary>Wiek kalibracji w dniach względem podanej chwili.</summary>
        public double AgeInDays(DateTime nowUtc) => (nowUtc - CreatedAtUtc).TotalDays;

        /// <summary>
        /// Sprawdza, czy sesja jest udokumentowana na tyle, by wynik dało się odtworzyć i obronić.
        ///
        /// <see cref="ProvenanceReport.Missing"/> to braki blokujące — bez nich pomiar nie jest
        /// przypisany do konkretnego przyrządu ani do konkretnej normy, więc nie jest spójny
        /// pomiarowo. <see cref="ProvenanceReport.Incomplete"/> to dane, których brak nie
        /// przekreśla wyniku, ale które trzeba podać, opisując stanowisko w publikacji.
        /// </summary>
        public ProvenanceReport CheckProvenance()
        {
            var report = new ProvenanceReport();

            if (string.IsNullOrWhiteSpace(Operator)) report.Missing.Add("osoba prowadząca wzorcowanie");

            if (Equipment == null)
            {
                report.Missing.Add("opis toru pomiarowego");
            }
            else
            {
                if (Equipment.SoundLevelMeter == null || !Equipment.SoundLevelMeter.IsIdentified)
                    report.Missing.Add("model i numer seryjny miernika poziomu dźwięku");
                else if (!Equipment.SoundLevelMeter.HasTraceability)
                    report.Missing.Add("świadectwo wzorcowania miernika (numer i data)");

                if (Equipment.Coupler == null || !Equipment.Coupler.IsIdentified)
                    report.Missing.Add("model i numer seryjny sprzęgacza");

                if (string.IsNullOrWhiteSpace(Equipment.CouplerStandard))
                    report.Missing.Add("norma sprzęgacza — RETSPL zależy od pary przetwornik + sprzęgacz");

                if (string.IsNullOrWhiteSpace(Equipment.FrequencyWeighting))
                    report.Missing.Add("ważenie częstotliwościowe miernika");

                if (string.IsNullOrWhiteSpace(Equipment.TimeWeighting))
                    report.Incomplete.Add("ważenie czasowe miernika");

                if (string.IsNullOrWhiteSpace(Equipment.MeasurementMode))
                    report.Incomplete.Add("odczytywana wielkość (SPL / Leq)");

                if (Equipment.CalibratorCheck == null ||
                    !Equipment.CalibratorCheck.ReadingBeforeSessionDbSpl.HasValue)
                    report.Missing.Add("sprawdzenie toru kalibratorem akustycznym przed sesją");
                else if (!Equipment.CalibratorCheck.ReadingAfterSessionDbSpl.HasValue)
                    report.Incomplete.Add("sprawdzenie kalibratorem po sesji — bez niego nie znasz dryfu toru");

                if (Equipment.Microphone == null || !Equipment.Microphone.IsIdentified)
                    report.Incomplete.Add("model i numer seryjny mikrofonu");
            }

            if (TransducerDetails == null || !TransducerDetails.IsIdentified)
                report.Missing.Add("model i numer seryjny przetwornika");

            if (Stimulus == null)
                report.Missing.Add("parametry syntezy bodźca");
            else if (string.IsNullOrWhiteSpace(Stimulus.TimingSource))
                report.Missing.Add("źródło czasów narastania i trwania bodźca");

            if (Standards == null)
            {
                report.Missing.Add("źródła wartości normatywnych");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Standards.Retspl))
                    report.Missing.Add("norma, z której pochodzą wartości RETSPL, wraz z wydaniem");

                if (string.IsNullOrWhiteSpace(Standards.LevelTolerance))
                    report.Missing.Add("źródło przyjętej tolerancji poziomu");

                if (string.IsNullOrWhiteSpace(Standards.SoundLevelMeter))
                    report.Incomplete.Add("norma miernika poziomu dźwięku");
            }

            if (Ambient == null || !Ambient.BackgroundNoiseDbA.HasValue)
                report.Incomplete.Add("poziom hałasu tła w pomieszczeniu");

            return report;
        }
    }
}
