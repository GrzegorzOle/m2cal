using System;

namespace M2Cal.Core
{
    /// <summary>
    /// Wzorzec pliku kalibracyjnego z wypełnionymi wszystkimi sekcjami. Służy dwóm rzeczom:
    /// dokumentacji formatu (opis nie może rozjechać się z kodem, bo jest z niego generowany)
    /// oraz autorom aplikacji docelowej, którzy muszą odczytać dokładnie te same pola.
    ///
    /// Wartości są zastępcze i jawnie oznaczone. Żadna z nich nie jest wartością normatywną —
    /// narzędzie takich nie zna i nie podstawia.
    /// </summary>
    public static class CalibrationTemplate
    {
        /// <summary>Tekst wstawiany tam, gdzie wartość musi podać operator.</summary>
        public const string Placeholder = "<uzupełnij>";

        public static CalibrationFile Create()
        {
            var chwila = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return new CalibrationFile
            {
                CreatedAtUtc = chwila,
                Operator = Placeholder,
                Transducer = Placeholder,
                Coupler = Placeholder,
                RefDbFs = -20.0,
                Notes = Placeholder,

                Device = new DeviceFingerprint
                {
                    DeviceId = Placeholder,
                    DeviceName = Placeholder,
                    SampleRate = 48000,
                    BitDepth = 24,
                    ChannelCount = 2,
                    EndpointVolumePercent = 100
                },

                TransducerDetails = new TransducerInfo
                {
                    Manufacturer = Placeholder,
                    Model = Placeholder,
                    SerialNumber = Placeholder,
                    CushionType = Placeholder
                },

                Equipment = new MeasurementChain
                {
                    SoundLevelMeter = new InstrumentInfo
                    {
                        Manufacturer = Placeholder,
                        Model = Placeholder,
                        SerialNumber = Placeholder,
                        ConformsToStandard = Placeholder,
                        CalibrationCertificate = Placeholder,
                        CalibratedOnUtc = chwila,
                        CalibrationValidUntilUtc = chwila.AddYears(1)
                    },
                    Microphone = new InstrumentInfo
                    {
                        Model = Placeholder,
                        SerialNumber = Placeholder
                    },
                    Coupler = new InstrumentInfo
                    {
                        Model = Placeholder,
                        SerialNumber = Placeholder
                    },
                    CouplerStandard = Placeholder,
                    FrequencyWeighting = "Z",
                    TimeWeighting = "S",
                    MeasurementMode = "SPL",
                    IntegrationTimeSeconds = null,
                    CalibratorCheck = new AcousticCalibratorCheck
                    {
                        Calibrator = new InstrumentInfo
                        {
                            Model = Placeholder,
                            SerialNumber = Placeholder
                        },
                        NominalLevelDbSpl = null,
                        NominalFrequencyHz = null,
                        ReadingBeforeSessionDbSpl = null,
                        ReadingAfterSessionDbSpl = null
                    }
                },

                Stimulus = new StimulusSettings
                {
                    RiseFallMs = ToneSynthesizer.RampSeconds * 1000.0,
                    PulseOnMs = ToneSynthesizer.PulseOnSeconds * 1000.0,
                    PulseOffMs = ToneSynthesizer.PulseOffSeconds * 1000.0,
                    WarbleDepthPercent = ToneSynthesizer.WarbleDepth * 100.0,
                    WarbleRateHz = ToneSynthesizer.WarbleRateHz,
                    Pulsed = false,
                    Warble = false,
                    SampleRate = 48000,
                    SynthesizerVersion = ToneSynthesizer.Version,
                    TimingSource = Placeholder
                },

                Ambient = new AmbientConditions
                {
                    BackgroundNoiseDbA = null,
                    TemperatureCelsius = null,
                    RelativeHumidityPercent = null,
                    AtmosphericPressureHpa = null,
                    Location = Placeholder
                },

                Standards = new StandardsReferences
                {
                    Retspl = Placeholder,
                    LevelTolerance = Placeholder,
                    StimulusTiming = Placeholder,
                    SoundLevelMeter = Placeholder,
                    Coupler = Placeholder,
                    AmbientNoise = Placeholder
                },

                Points =
                {
                    new CalibrationPoint
                    {
                        FrequencyHz = 1000,
                        Ear = "L",
                        StimulusDbFs = -20,
                        MeasuredSpl = 74,
                        MeasuredAtUtc = chwila,
                        Note = null
                    }
                },

                Verify = new VerifyResult
                {
                    PerformedAtUtc = chwila,
                    Passed = false,
                    MaxDeviationDb = 0,
                    ToleranceDb = 0,
                    RetsplSource = Placeholder
                }
            };
        }
    }
}
