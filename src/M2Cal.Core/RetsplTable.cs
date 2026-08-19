using System;
using System.Collections.Generic;
using System.Linq;

namespace M2Cal.Core
{
    /// <summary>
    /// Tabela RETSPL (Reference Equivalent Threshold Sound Pressure Level) — poziom akustyczny
    /// odpowiadający 0 dB HL dla konkretnej pary przetwornik + sprzęgacz. Wartości pochodzą
    /// z właściwej części serii ISO 389 i są danymi wejściowymi, nie wynikiem obliczeń:
    /// narzędzie ich nie wylicza ani nie zgaduje.
    /// </summary>
    public sealed class RetsplTable
    {
        /// <summary>Przetwornik, dla którego obowiązują te wartości, np. „TDH-39".</summary>
        public string Transducer { get; set; }

        /// <summary>Sprzęgacz, np. „NBS 9A".</summary>
        public string Coupler { get; set; }

        /// <summary>Źródło danych — konkretna część normy, np. „ISO 389-1:2017".</summary>
        public string Source { get; set; }

        /// <summary>Częstotliwość w Hz → RETSPL w dB SPL.</summary>
        public Dictionary<string, double> Values { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Ustawiane na true w plikach przykładowych. Wartości poglądowe nie nadają się do
        /// badań ludzi — <see cref="DeviceGate"/> odrzuca kalibrację opartą o taką tabelę.
        /// </summary>
        public bool Example { get; set; }

        public bool TryGetRetspl(double frequencyHz, out double retsplDb)
        {
            retsplDb = 0.0;

            foreach (var kv in Values)
            {
                if (double.TryParse(kv.Key, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out double f)
                    && Math.Abs(f - frequencyHz) < 0.5)
                {
                    retsplDb = kv.Value;
                    return true;
                }
            }

            return false;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public IEnumerable<double> Frequencies =>
            Values.Keys
                  .Select(k => double.TryParse(k, System.Globalization.NumberStyles.Float,
                                               System.Globalization.CultureInfo.InvariantCulture, out double f)
                               ? (double?)f : null)
                  .Where(f => f.HasValue)
                  .Select(f => f.Value)
                  .OrderBy(f => f);
    }
}
