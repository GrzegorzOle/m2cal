using System;
using System.Collections.Generic;
using System.Linq;

namespace M2Cal.Core
{
    public sealed class GateResult
    {
        /// <summary>Jedyny warunek dopuszczający do badania.</summary>
        public bool Allowed { get; set; }

        /// <summary>Powody odrzucenia — puste, gdy <see cref="Allowed"/> jest true.</summary>
        public List<string> Blockers { get; } = new List<string>();

        /// <summary>Zastrzeżenia, które nie blokują, ale operator ma je zobaczyć.</summary>
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Bramka dopuszczenia do badania. Aplikacja docelowa wywołuje ją przed każdą sesją
    /// i dopiero <see cref="GateResult.Allowed"/> == true pozwala prezentować bodźce.
    ///
    /// Bramka widzi wyłącznie stan software'owy. Pozycja analogowych pokręteł MOTU M2
    /// (słuchawkowego i Monitor) jest programowo niewidoczna i pozostaje pod kontrolą
    /// procedury organizacyjnej.
    /// </summary>
    public static class DeviceGate
    {
        public static GateResult Check(CalibrationFile calibration,
                                       DeviceFingerprint current,
                                       DateTime nowUtc,
                                       RetsplTable retspl = null,
                                       int maxAgeDays = CalibrationFile.DefaultMaxAgeDays)
        {
            var result = new GateResult();

            if (calibration == null)
            {
                result.Blockers.Add("brak pliku kalibracyjnego");
                result.Allowed = false;
                return result;
            }

            if (calibration.SchemaVersion != CalibrationFile.CurrentSchemaVersion)
                result.Blockers.Add($"nieobsługiwana wersja schematu pliku ({calibration.SchemaVersion}, oczekiwano {CalibrationFile.CurrentSchemaVersion})");

            if (calibration.SynthesizerVersion != 1)
                result.Blockers.Add($"plik opisuje inną wersję syntezy bodźców ({calibration.SynthesizerVersion}) — kalibracja nie opisuje aktualnego kodu DSP");

            if (calibration.Points == null || calibration.Points.Count == 0)
                result.Blockers.Add("mapa kalibracyjna jest pusta");

            if (calibration.Device == null)
                result.Blockers.Add("plik kalibracyjny nie zawiera odcisku urządzenia");
            else if (!calibration.Device.Matches(current, out string reason))
                result.Blockers.Add("konfiguracja toru różni się od wzorcowanej: " + reason);

            if (current != null && current.EndpointVolumePercent != 100)
                result.Blockers.Add($"głośność endpointu wynosi {current.EndpointVolumePercent} %, wymagane 100 % — cała regulacja poziomu odbywa się w dBFS");

            double age = calibration.AgeInDays(nowUtc);
            if (age < 0)
                result.Blockers.Add("data kalibracji jest w przyszłości — sprawdź zegar systemowy");
            else if (age > maxAgeDays)
                result.Blockers.Add($"kalibracja przeterminowana: {age:0} dni (limit {maxAgeDays})");
            else if (age > maxAgeDays * 0.9)
                result.Warnings.Add($"kalibracja wygasa za {maxAgeDays - age:0} dni");

            if (calibration.Verify == null)
                result.Blockers.Add("brak kontroli verify — kalibracja nie została domknięta pomiarem sprawdzającym");
            else if (!calibration.Verify.Passed)
                result.Blockers.Add($"kontrola verify negatywna (największe odchylenie {calibration.Verify.MaxDeviationDb:0.0} dB przy tolerancji ±{calibration.Verify.ToleranceDb:0.0} dB)");

            if (retspl != null && retspl.Example)
                result.Blockers.Add("użyto przykładowej tabeli RETSPL — przed badaniami wymagane są dane z właściwej części ISO 389");

            if (retspl != null && !string.IsNullOrWhiteSpace(calibration.Transducer)
                && !string.IsNullOrWhiteSpace(retspl.Transducer)
                && !string.Equals(calibration.Transducer.Trim(), retspl.Transducer.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                result.Blockers.Add($"tabela RETSPL dotyczy przetwornika {retspl.Transducer}, a kalibracja {calibration.Transducer}");
            }

            if (calibration.Points != null)
            {
                foreach (var group in calibration.Points.GroupBy(p => new { p.FrequencyHz, p.Ear }))
                {
                    var spread = group.Max(p => p.SplAtFullScale) - group.Min(p => p.SplAtFullScale);
                    if (spread > 2.0)
                        result.Warnings.Add($"{group.Key.FrequencyHz:0} Hz / {group.Key.Ear}: rozrzut czułości {spread:0.0} dB między punktami — możliwa nieliniowość toru");
                }
            }

            result.Allowed = result.Blockers.Count == 0;
            return result;
        }
    }
}
