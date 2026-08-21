using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using M2Cal.Core;

namespace M2Cal.Cli
{
    public static class Program
    {
        public const int ExitOk = 0;
        public const int ExitError = 1;
        public const int ExitGateRejected = 3;
        public const int ExitVerifyFailed = 4;

        private static readonly double[] DefaultFrequencies = { 500, 1000, 2000, 4000 };
        private static readonly double[] DefaultScreeningFrequencies = { 1000, 2000 };

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                PrintUsage();
                return args.Length == 0 ? ExitError : ExitOk;
            }

            var options = ArgMap.Parse(args, 1);

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "devices": return Devices();
                    case "check": return Check(options);
                    case "tone": return Tone(options);
                    case "calibrate": return Calibrate(options);
                    case "verify": return Verify(options);
                    case "screen": return Screen(options);
                    case "selftest": return RunSelfTest();
                    case "example": return Example(options);
                    default:
                        Console.Error.WriteLine($"nieznana komenda: {args[0]}");
                        PrintUsage();
                        return ExitError;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("błąd: " + ex.Message);
                return ExitError;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"m2cal — wzorcowanie toru DAC MOTU M2

  devices                                  lista urządzeń odtwarzających (MOTU M2 oznaczone *)
  check [--cal plik] [--retspl plik]       odcisk toru; z --cal bramka dopuszczenia
  tone --freq 1000 --dbfs -20 --ch L       ton diagnostyczny (--pulsed, --warble, --seconds)
  calibrate --freqs 500,1000,2000,4000     sesja wzorcowania, zapis mapy pomiarów
            --ref-dbfs -20 --transducer TDH-39 --operator ""Jan K."" [--out plik]
  verify --cal plik --retspl plik --hl 20  kontrola poziomów w dB HL (--tolerance 3)
  screen --cal plik --retspl plik --hl 20  podgląd bodźca przesiewowego
  selftest                                 testy matematyki, bez sprzętu, każdy OS
  example [--out plik]                     wzorzec pliku kalibracyjnego z wszystkimi polami

Kody wyjścia: 0 OK, 1 błąd, 3 bramka odrzuciła, 4 verify negatywny.");
        }

        // ---------------------------------------------------------------- devices

        private static int Devices()
        {
            var devices = AudioEngine.Enumerate();

            foreach (var d in devices)
            {
                string marks = (d.LooksLikeMotuM2 ? " *" : "") + (d.IsDefault ? " [domyślne]" : "");
                Console.WriteLine($"{d.Name}{marks}");
                Console.WriteLine($"    {d.SampleRate} Hz / {d.BitDepth} bit / {d.ChannelCount} kan. / głośność {d.VolumePercent} %");
                Console.WriteLine($"    {d.Id}");
            }

            if (devices.All(d => !d.LooksLikeMotuM2))
                Console.WriteLine("\nNie wykryto MOTU M2 — wskaż urządzenie przez --device.");

            return ExitOk;
        }

        // ---------------------------------------------------------------- check

        private static int Check(ArgMap options)
        {
            var device = AudioEngine.Resolve(options.String("device"));
            var fingerprint = device.ToFingerprint();

            Console.WriteLine($"Urządzenie : {device.Name}");
            Console.WriteLine($"Tor        : {device.SampleRate} Hz / {device.BitDepth} bit / {device.ChannelCount} kan.");
            Console.WriteLine($"Głośność   : {device.VolumePercent} %");

            if (device.VolumePercent != 100)
                Console.WriteLine("  ! głośność endpointu musi wynosić 100 % — regulacja poziomu odbywa się wyłącznie w dBFS");

            string calPath = options.String("cal");
            if (string.IsNullOrWhiteSpace(calPath))
            {
                Console.WriteLine("\nBez --cal to tylko odczyt presetów; bramka dopuszczenia nie była sprawdzana.");
                return ExitOk;
            }

            var calibration = LoadCalibration(calPath);
            var retspl = LoadRetsplOrNull(options.String("retspl"));
            var gate = DeviceGate.Check(calibration, fingerprint, DateTime.UtcNow, retspl);

            Console.WriteLine();
            PrintGate(gate);

            return gate.Allowed ? ExitOk : ExitGateRejected;
        }

        private static void PrintGate(GateResult gate)
        {
            foreach (var warning in gate.Warnings)
                Console.WriteLine("  ~ " + warning);

            foreach (var blocker in gate.Blockers)
                Console.WriteLine("  ! " + blocker);

            Console.WriteLine(gate.Allowed
                ? "DOPUSZCZONE — tor zgodny z kalibracją."
                : "ODRZUCONE — badania nie wolno rozpocząć.");
        }

        // ---------------------------------------------------------------- tone

        private static int Tone(ArgMap options)
        {
            var device = AudioEngine.Resolve(options.String("device"));

            var synth = new ToneSynthesizer(device.SampleRate)
            {
                FrequencyHz = options.Double("freq", 1000),
                LevelDbFs = options.Double("dbfs", -20),
                Ear = options.Ear("ch", Core.Ear.Left),
                Pulsed = options.Has("pulsed"),
                Warble = options.Has("warble")
            };

            double seconds = options.Double("seconds", 3);

            Console.WriteLine($"{device.Name}: {synth.FrequencyHz:0} Hz, {synth.LevelDbFs:0.0} dBFS, " +
                              $"kanał {synth.Ear.ToCode()}{(synth.Pulsed ? ", pulsowany" : "")}{(synth.Warble ? ", wobbulowany" : "")}");

            AudioEngine.Play(device, synth, TimeSpan.FromSeconds(seconds));
            return ExitOk;
        }

        // ---------------------------------------------------------------- calibrate

        private static int Calibrate(ArgMap options)
        {
            var device = AudioEngine.Resolve(options.String("device"));

            if (device.VolumePercent != 100)
            {
                Console.Error.WriteLine($"głośność endpointu wynosi {device.VolumePercent} %. " +
                                        "Wzorcowanie wymaga 100 % — ustaw i powtórz.");
                return ExitError;
            }

            double[] frequencies = options.Doubles("freqs", DefaultFrequencies);
            double refDbFs = options.Double("ref-dbfs", -20);
            var ears = new[] { Core.Ear.Left, Core.Ear.Right };

            var calibration = new CalibrationFile
            {
                CreatedAtUtc = DateTime.UtcNow,
                Operator = options.String("operator"),
                Transducer = options.String("transducer"),
                Coupler = options.String("coupler"),
                RefDbFs = refDbFs,
                Device = device.ToFingerprint()
            };

            Console.WriteLine($"Wzorcowanie: {device.Name}, {device.SampleRate} Hz, ton odniesienia {refDbFs:0.0} dBFS.");
            Console.WriteLine("Dla każdego punktu odczytaj poziom z miernika i wpisz w dB SPL (pusta linia = pomiń).\n");

            foreach (var ear in ears)
            {
                foreach (double frequency in frequencies)
                {
                    var synth = new ToneSynthesizer(device.SampleRate)
                    {
                        FrequencyHz = frequency,
                        LevelDbFs = refDbFs,
                        Ear = ear
                    };

                    double? measured;
                    using (AudioEngine.Start(device, synth))
                    {
                        measured = AskForSpl($"{frequency,6:0} Hz  kanał {ear.ToCode()}  {refDbFs:0.0} dBFS");
                    }

                    if (measured == null)
                    {
                        Console.WriteLine("       pominięto");
                        continue;
                    }

                    calibration.Points.Add(new CalibrationPoint
                    {
                        FrequencyHz = frequency,
                        Ear = ear.ToCode(),
                        StimulusDbFs = refDbFs,
                        MeasuredSpl = measured.Value,
                        MeasuredAtUtc = DateTime.UtcNow
                    });

                    Console.WriteLine($"       czułość toru: {measured.Value - refDbFs:0.0} dB SPL przy pełnej skali");
                }
            }

            if (calibration.Points.Count == 0)
            {
                Console.Error.WriteLine("nie zapisano żadnego punktu — plik nie powstał");
                return ExitError;
            }

            string outPath = options.String("out", CalibrationStore.DefaultFileName);
            File.WriteAllText(outPath, CalibrationStore.Serialize(calibration));

            Console.WriteLine($"\nZapisano {calibration.Points.Count} punktów do {outPath}.");
            Console.WriteLine("Kalibracja NIE jest jeszcze domknięta — uruchom `m2cal verify`, bez tego bramka odrzuci plik.");
            return ExitOk;
        }

        // ---------------------------------------------------------------- verify

        private static int Verify(ArgMap options)
        {
            var device = AudioEngine.Resolve(options.String("device"));
            var calibration = LoadCalibration(RequirePath(options, "cal"));
            var retspl = LoadRetspl(RequirePath(options, "retspl"));

            double hearingLevel = options.Double("hl", 20);
            double tolerance = options.Double("tolerance", 3);
            double[] frequencies = options.Doubles("freqs", calibration.Frequencies.ToArray());

            var result = new VerifyResult
            {
                PerformedAtUtc = DateTime.UtcNow,
                ToleranceDb = tolerance,
                RetsplSource = retspl.Source
            };

            Console.WriteLine($"Kontrola przy {hearingLevel:0} dB HL, tolerancja ±{tolerance:0.0} dB.\n");

            foreach (var ear in new[] { Core.Ear.Left, Core.Ear.Right })
            {
                foreach (double frequency in frequencies)
                {
                    if (!calibration.TryGetRequiredDbFs(frequency, ear, hearingLevel, retspl,
                                                        out double dbFs, out string error))
                    {
                        Console.WriteLine($"{frequency,6:0} Hz  kanał {ear.ToCode()}  pominięto: {error}");
                        continue;
                    }

                    retspl.TryGetRetspl(frequency, out double retsplDb);
                    double expectedSpl = retsplDb + hearingLevel;

                    var synth = new ToneSynthesizer(device.SampleRate)
                    {
                        FrequencyHz = frequency,
                        LevelDbFs = dbFs,
                        Ear = ear
                    };

                    double? measured;
                    using (AudioEngine.Start(device, synth))
                    {
                        measured = AskForSpl($"{frequency,6:0} Hz  kanał {ear.ToCode()}  {dbFs:0.0} dBFS  " +
                                             $"oczekiwane {expectedSpl:0.0} dB SPL");
                    }

                    if (measured == null)
                    {
                        Console.WriteLine("       pominięto");
                        continue;
                    }

                    var point = new VerifyPoint
                    {
                        FrequencyHz = frequency,
                        Ear = ear.ToCode(),
                        HearingLevelDb = hearingLevel,
                        ExpectedSpl = expectedSpl,
                        MeasuredSpl = measured.Value
                    };

                    result.Points.Add(point);
                    Console.WriteLine($"       odchylenie {point.DeviationDb,6:+0.0;-0.0;0.0} dB  " +
                                      $"{(Math.Abs(point.DeviationDb) <= tolerance ? "OK" : "POZA TOLERANCJĄ")}");
                }
            }

            if (result.Points.Count == 0)
            {
                Console.Error.WriteLine("nie zmierzono żadnego punktu — wynik kontroli nie powstał");
                return ExitError;
            }

            result.MaxDeviationDb = result.Points.Max(p => Math.Abs(p.DeviationDb));
            result.Passed = result.MaxDeviationDb <= tolerance;

            calibration.Verify = result;
            File.WriteAllText(options.String("cal"), CalibrationStore.Serialize(calibration));

            Console.WriteLine($"\nNajwiększe odchylenie: {result.MaxDeviationDb:0.0} dB.");
            Console.WriteLine(result.Passed ? "VERIFY POZYTYWNY" : "VERIFY NEGATYWNY");

            return result.Passed ? ExitOk : ExitVerifyFailed;
        }

        // ---------------------------------------------------------------- screen

        private static int Screen(ArgMap options)
        {
            var device = AudioEngine.Resolve(options.String("device"));
            var calibration = LoadCalibration(RequirePath(options, "cal"));
            var retspl = LoadRetspl(RequirePath(options, "retspl"));

            var gate = DeviceGate.Check(calibration, device.ToFingerprint(), DateTime.UtcNow, retspl);
            if (!gate.Allowed)
            {
                PrintGate(gate);
                return ExitGateRejected;
            }

            double hearingLevel = options.Double("hl", 20);
            double[] frequencies = options.Doubles("freqs", DefaultScreeningFrequencies);
            double seconds = options.Double("seconds", 2);

            foreach (var ear in new[] { Core.Ear.Left, Core.Ear.Right })
            {
                foreach (double frequency in frequencies)
                {
                    if (!calibration.TryGetRequiredDbFs(frequency, ear, hearingLevel, retspl,
                                                        out double dbFs, out string error))
                    {
                        Console.WriteLine($"{frequency,6:0} Hz  kanał {ear.ToCode()}  pominięto: {error}");
                        continue;
                    }

                    Console.WriteLine($"{frequency,6:0} Hz  kanał {ear.ToCode()}  {hearingLevel:0} dB HL  →  {dbFs:0.0} dBFS");

                    var synth = new ToneSynthesizer(device.SampleRate)
                    {
                        FrequencyHz = frequency,
                        LevelDbFs = dbFs,
                        Ear = ear,
                        Pulsed = true
                    };

                    AudioEngine.Play(device, synth, TimeSpan.FromSeconds(seconds));
                }
            }

            return ExitOk;
        }

        // ---------------------------------------------------------------- selftest

        private static int RunSelfTest()
        {
            var report = SelfTest.Run();

            foreach (var testCase in report.Cases)
            {
                Console.WriteLine($"[{(testCase.Passed ? "OK  " : "BŁĄD")}] {testCase.Name}" +
                                  (string.IsNullOrEmpty(testCase.Detail) ? "" : "  — " + testCase.Detail));
            }

            Console.WriteLine($"\n{report.PassedCount}/{report.Cases.Count} testów przeszło.");
            return report.AllPassed ? ExitOk : ExitError;
        }

        // ---------------------------------------------------------------- example

        /// <summary>
        /// Wypisuje wzorzec pliku kalibracyjnego. Dokumentacja formatu jest z niego generowana,
        /// więc opis nie może rozjechać się z tym, co narzędzie faktycznie zapisuje.
        /// </summary>
        private static int Example(ArgMap options)
        {
            string json = CalibrationStore.Serialize(CalibrationTemplate.Create());

            string outPath = options.String("out");
            if (string.IsNullOrWhiteSpace(outPath))
                Console.WriteLine(json);
            else
                File.WriteAllText(outPath, json);

            return ExitOk;
        }

        // ---------------------------------------------------------------- pomocnicze

        private static double? AskForSpl(string prompt)
        {
            while (true)
            {
                Console.Write(prompt + "   zmierzone dB SPL: ");
                string input = Console.ReadLine();

                if (input == null || string.IsNullOrWhiteSpace(input)) return null;

                if (double.TryParse(input.Trim().Replace(',', '.'), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out double value))
                    return value;

                Console.WriteLine("       to nie liczba — wpisz odczyt z miernika albo Enter, żeby pominąć");
            }
        }

        private static string RequirePath(ArgMap options, string key)
        {
            string path = options.String(key);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"wymagany parametr --{key}");
            return path;
        }

        private static CalibrationFile LoadCalibration(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"nie znaleziono pliku kalibracyjnego: {path}");

            return CalibrationStore.Deserialize(File.ReadAllText(path));
        }

        private static RetsplTable LoadRetspl(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"nie znaleziono tabeli RETSPL: {path}");

            return CalibrationStore.DeserializeRetspl(File.ReadAllText(path));
        }

        private static RetsplTable LoadRetsplOrNull(string path) =>
            string.IsNullOrWhiteSpace(path) ? null : LoadRetspl(path);
    }
}
