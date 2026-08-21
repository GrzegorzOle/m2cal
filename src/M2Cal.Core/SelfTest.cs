using System;
using System.Collections.Generic;

namespace M2Cal.Core
{
    public sealed class SelfTestCase
    {
        public string Name { get; set; }
        public bool Passed { get; set; }
        public string Detail { get; set; }
    }

    public sealed class SelfTestReport
    {
        public List<SelfTestCase> Cases { get; } = new List<SelfTestCase>();
        public bool AllPassed => Cases.TrueForAll(c => c.Passed);
        public int PassedCount => Cases.FindAll(c => c.Passed).Count;
    }

    /// <summary>
    /// Testy matematyki wzorcowania. Żyją w rdzeniu, żeby dokładnie ten sam zestaw dało się
    /// uruchomić z CLI (także poza Windows, bez sprzętu) i z GUI przed sesją wzorcowania.
    /// </summary>
    public static class SelfTest
    {
        public static SelfTestReport Run()
        {
            var report = new SelfTestReport();

            Check(report, "dBFS → amplituda: 0 dBFS = pełna skala",
                  () => Near(ToneSynthesizer.DbFsToAmplitude(0.0), 1.0, 1e-12));

            Check(report, "dBFS → amplituda: −20 dBFS = 0,1",
                  () => Near(ToneSynthesizer.DbFsToAmplitude(-20.0), 0.1, 1e-12));

            Check(report, "amplituda → dBFS: obieg zamknięty",
                  () => Near(ToneSynthesizer.AmplitudeToDbFs(ToneSynthesizer.DbFsToAmplitude(-37.5)), -37.5, 1e-9));

            Check(report, "RMS zsyntezowanego sinusa −20 dBFS ≈ 0,0707", () =>
            {
                var synth = new ToneSynthesizer(48000) { FrequencyHz = 1000, LevelDbFs = -20, Ear = Ear.Left };
                double rms = RmsOfChannel(synth, 48000, left: true, skipFrames: 4800);
                return Near(rms, 0.1 / Math.Sqrt(2.0), 1e-3);
            });

            Check(report, "kanał L nie przecieka na kanał P", () =>
            {
                var synth = new ToneSynthesizer(48000) { FrequencyHz = 1000, LevelDbFs = -6, Ear = Ear.Left };
                return RmsOfChannel(synth, 24000, left: false, skipFrames: 0) == 0.0;
            });

            Check(report, "kanał P nie przecieka na kanał L", () =>
            {
                var synth = new ToneSynthesizer(48000) { FrequencyHz = 1000, LevelDbFs = -6, Ear = Ear.Right };
                return RmsOfChannel(synth, 24000, left: true, skipFrames: 0) == 0.0;
            });

            Check(report, "obwiednia startuje od ciszy (brak trzasku)", () =>
            {
                var synth = new ToneSynthesizer(48000) { FrequencyHz = 1000, LevelDbFs = 0, Ear = Ear.Both };
                var buffer = new float[2 * 16];
                synth.Render(buffer, 0, 16);
                return Math.Abs(buffer[0]) < 1e-6;
            });

            Check(report, "ton pulsowany ma ciszę w przerwie", () =>
            {
                var synth = new ToneSynthesizer(48000)
                {
                    FrequencyHz = 1000, LevelDbFs = -6, Ear = Ear.Both, Pulsed = true
                };
                var buffer = new float[2 * 48000];
                synth.Render(buffer, 0, 48000);

                // środek pierwszej przerwy: 225 ms + połowa z 225 ms
                int idx = (int)(48000 * (ToneSynthesizer.PulseOnSeconds + ToneSynthesizer.PulseOffSeconds / 2)) * 2;
                return Math.Abs(buffer[idx]) < 1e-9;
            });

            Check(report, "przykład z dokumentacji: 20 dB HL → −67 dBFS", () =>
            {
                var cal = ExampleCalibration();
                var retspl = new RetsplTable { Values = { ["1000"] = 7.0 } };
                bool ok = cal.TryGetRequiredDbFs(1000, Ear.Left, 20, retspl, out double dbFs, out _);
                return ok && Near(dbFs, -67.0, 1e-9);
            });

            Check(report, "czułość toru niezależna od poziomu bodźca", () =>
            {
                var cal = ExampleCalibration();
                cal.Points.Add(new CalibrationPoint
                {
                    FrequencyHz = 1000, Ear = "L", StimulusDbFs = -40, MeasuredSpl = 54
                });
                bool ok = cal.TryGetSensitivity(1000, Ear.Left, out double spl, out double spread);
                return ok && Near(spl, 94.0, 1e-9) && Near(spread, 0.0, 1e-9);
            });

            Check(report, "brak punktu w mapie nie jest interpolowany", () =>
            {
                var cal = ExampleCalibration();
                var retspl = new RetsplTable { Values = { ["3000"] = 10.0 } };
                return !cal.TryGetRequiredDbFs(3000, Ear.Left, 20, retspl, out _, out string err)
                       && !string.IsNullOrEmpty(err);
            });

            Check(report, "poziom ponad pełną skalą jest odrzucany", () =>
            {
                var cal = ExampleCalibration();
                var retspl = new RetsplTable { Values = { ["1000"] = 7.0 } };
                return !cal.TryGetRequiredDbFs(1000, Ear.Left, 120, retspl, out _, out _);
            });

            Check(report, "bramka dopuszcza poprawną kalibrację", () =>
            {
                var cal = ExampleCalibration();
                var gate = DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(10));
                return gate.Allowed;
            });

            Check(report, "bramka odrzuca kalibrację przeterminowaną", () =>
            {
                var cal = ExampleCalibration();
                var gate = DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(400));
                return !gate.Allowed;
            });

            Check(report, "bramka odrzuca zmienioną częstotliwość próbkowania", () =>
            {
                var cal = ExampleCalibration();
                var now = new DeviceFingerprint
                {
                    DeviceId = cal.Device.DeviceId,
                    SampleRate = 44100,
                    ChannelCount = cal.Device.ChannelCount,
                    EndpointVolumePercent = 100
                };
                return !DeviceGate.Check(cal, now, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "bramka odrzuca głośność endpointu poniżej 100 %", () =>
            {
                var cal = ExampleCalibration();
                var now = new DeviceFingerprint
                {
                    DeviceId = cal.Device.DeviceId,
                    SampleRate = cal.Device.SampleRate,
                    ChannelCount = cal.Device.ChannelCount,
                    EndpointVolumePercent = 80
                };
                return !DeviceGate.Check(cal, now, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "bramka odrzuca brak kontroli verify", () =>
            {
                var cal = ExampleCalibration();
                cal.Verify = null;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "bramka odrzuca przykładową tabelę RETSPL", () =>
            {
                var cal = ExampleCalibration();
                var retspl = new RetsplTable { Example = true, Values = { ["1000"] = 7.0 } };
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1), retspl).Allowed;
            });

            Check(report, "plik kalibracyjny przechodzi obieg JSON bez strat", () =>
            {
                var cal = ExampleCalibration();
                var restored = CalibrationStore.Deserialize(CalibrationStore.Serialize(cal));
                return restored.Points.Count == cal.Points.Count
                       && Near(restored.Points[0].MeasuredSpl, cal.Points[0].MeasuredSpl, 1e-9)
                       && Near(restored.Points[0].StimulusDbFs, cal.Points[0].StimulusDbFs, 1e-9)
                       && restored.Device.DeviceId == cal.Device.DeviceId
                       && restored.Verify != null && restored.Verify.Passed;
            });

            Check(report, "metadane stanowiska przechodzą obieg JSON bez strat", () =>
            {
                var cal = ExampleCalibration();
                var restored = CalibrationStore.Deserialize(CalibrationStore.Serialize(cal));

                return restored.Equipment?.SoundLevelMeter?.SerialNumber == "SLM-1"
                       && restored.Equipment.CouplerStandard == "selftest"
                       && restored.Equipment.FrequencyWeighting == "Z"
                       && restored.TransducerDetails?.SerialNumber == "SELFTEST-T1"
                       && restored.Standards?.Retspl == "selftest"
                       && Near(restored.Stimulus.RiseFallMs, ToneSynthesizer.RampSeconds * 1000, 1e-9)
                       && restored.Stimulus.SynthesizerVersion == ToneSynthesizer.Version;
            });

            Check(report, "kompletne udokumentowanie stanowiska nie zgłasza braków",
                  () => ExampleCalibration().CheckProvenance().IsComplete);

            Check(report, "brak miernika blokuje dopuszczenie", () =>
            {
                var cal = ExampleCalibration();
                cal.Equipment.SoundLevelMeter = null;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "brak świadectwa wzorcowania miernika blokuje dopuszczenie", () =>
            {
                var cal = ExampleCalibration();
                cal.Equipment.SoundLevelMeter.CalibrationCertificate = null;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "brak normy RETSPL blokuje dopuszczenie", () =>
            {
                var cal = ExampleCalibration();
                cal.Standards.Retspl = null;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "brak normy sprzęgacza blokuje dopuszczenie", () =>
            {
                var cal = ExampleCalibration();
                cal.Equipment.CouplerStandard = null;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "brak źródła czasów bodźca blokuje dopuszczenie", () =>
            {
                var cal = ExampleCalibration();
                cal.Stimulus.TimingSource = null;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "dryf toru pomiarowego blokuje dopuszczenie", () =>
            {
                var cal = ExampleCalibration();
                cal.Equipment.CalibratorCheck.ReadingAfterSessionDbSpl = 95.4;   // 1,4 dB dryfu
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "brak hałasu tła tylko ostrzega, nie blokuje", () =>
            {
                var cal = ExampleCalibration();
                cal.Ambient = null;
                var gate = DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1));
                return gate.Allowed && gate.Warnings.Count > 0;
            });

            Check(report, "plik w starej wersji schematu jest odrzucany", () =>
            {
                var cal = ExampleCalibration();
                cal.SchemaVersion = 1;
                return !DeviceGate.Check(cal, cal.Device, cal.CreatedAtUtc.AddDays(1)).Allowed;
            });

            Check(report, "parametry bodźca odczytane z syntezatora zgadzają się z kodem", () =>
            {
                var synth = new ToneSynthesizer(48000) { Pulsed = true, Warble = false };
                var settings = StimulusSettings.FromSynthesizer(synth);

                return Near(settings.RiseFallMs, 25.0, 1e-9)
                       && Near(settings.PulseOnMs, 225.0, 1e-9)
                       && Near(settings.PulseOffMs, 225.0, 1e-9)
                       && Near(settings.WarbleDepthPercent, 5.0, 1e-9)
                       && Near(settings.WarbleRateHz, 5.0, 1e-9)
                       && settings.Pulsed && !settings.Warble
                       && settings.SampleRate == 48000
                       && settings.SynthesizerVersion == ToneSynthesizer.Version;
            });

            return report;
        }

        /// <summary>Kalibracja użyta w testach — odpowiada przykładowi z dokumentacji.</summary>
        private static CalibrationFile ExampleCalibration()
        {
            var created = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

            return new CalibrationFile
            {
                CreatedAtUtc = created,
                Operator = "selftest",
                Transducer = "TDH-39",
                Coupler = "NBS 9A",
                RefDbFs = -20,
                Device = new DeviceFingerprint
                {
                    DeviceId = "{selftest-device}",
                    DeviceName = "MOTU M2",
                    SampleRate = 48000,
                    BitDepth = 24,
                    ChannelCount = 2,
                    EndpointVolumePercent = 100
                },
                Points =
                {
                    new CalibrationPoint
                    {
                        FrequencyHz = 1000, Ear = "L", StimulusDbFs = -20,
                        MeasuredSpl = 74, MeasuredAtUtc = created
                    }
                },
                Verify = new VerifyResult
                {
                    PerformedAtUtc = created,
                    Passed = true,
                    MaxDeviationDb = 0.8,
                    ToleranceDb = 3.0
                },
                TransducerDetails = new TransducerInfo
                {
                    Model = "TDH-39", SerialNumber = "SELFTEST-T1", CushionType = "supraauralne"
                },
                Equipment = new MeasurementChain
                {
                    SoundLevelMeter = new InstrumentInfo
                    {
                        Model = "SELFTEST-SLM", SerialNumber = "SLM-1",
                        ConformsToStandard = "selftest", CalibrationCertificate = "SELFTEST/1",
                        CalibratedOnUtc = created.AddDays(-30)
                    },
                    Microphone = new InstrumentInfo { Model = "SELFTEST-MIC", SerialNumber = "MIC-1" },
                    Coupler = new InstrumentInfo { Model = "SELFTEST-COUPLER", SerialNumber = "C-1" },
                    CouplerStandard = "selftest",
                    FrequencyWeighting = "Z",
                    TimeWeighting = "S",
                    MeasurementMode = "SPL",
                    CalibratorCheck = new AcousticCalibratorCheck
                    {
                        Calibrator = new InstrumentInfo { Model = "SELFTEST-CAL", SerialNumber = "CAL-1" },
                        ReadingBeforeSessionDbSpl = 94.0,
                        ReadingAfterSessionDbSpl = 94.1
                    }
                },
                Stimulus = new StimulusSettings
                {
                    RiseFallMs = ToneSynthesizer.RampSeconds * 1000,
                    PulseOnMs = ToneSynthesizer.PulseOnSeconds * 1000,
                    PulseOffMs = ToneSynthesizer.PulseOffSeconds * 1000,
                    SampleRate = 48000,
                    SynthesizerVersion = ToneSynthesizer.Version,
                    TimingSource = "selftest"
                },
                Ambient = new AmbientConditions { BackgroundNoiseDbA = 25.0 },
                Standards = new StandardsReferences
                {
                    Retspl = "selftest", LevelTolerance = "selftest", SoundLevelMeter = "selftest"
                }
            };
        }

        private static double RmsOfChannel(ToneSynthesizer synth, int frames, bool left, int skipFrames)
        {
            var buffer = new float[(frames + skipFrames) * 2];
            synth.Render(buffer, 0, frames + skipFrames);

            double sum = 0.0;
            for (int i = skipFrames; i < frames + skipFrames; i++)
            {
                double v = buffer[i * 2 + (left ? 0 : 1)];
                sum += v * v;
            }

            return Math.Sqrt(sum / frames);
        }

        private static bool Near(double a, double b, double tolerance) => Math.Abs(a - b) <= tolerance;

        private static void Check(SelfTestReport report, string name, Func<bool> body)
        {
            try
            {
                report.Cases.Add(new SelfTestCase { Name = name, Passed = body() });
            }
            catch (Exception ex)
            {
                report.Cases.Add(new SelfTestCase { Name = name, Passed = false, Detail = ex.Message });
            }
        }
    }
}
