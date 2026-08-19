using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace M2Cal.Cli
{
    /// <summary>Proste parsowanie argumentów w stylu <c>--klucz wartość</c> oraz flag <c>--flaga</c>.</summary>
    public sealed class ArgMap
    {
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static ArgMap Parse(string[] args, int startIndex)
        {
            var map = new ArgMap();

            for (int i = startIndex; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;

                string key = args[i].Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    map._values[key] = args[++i];
                }
                else
                {
                    map._flags.Add(key);
                }
            }

            return map;
        }

        public bool Has(string key) => _flags.Contains(key) || _values.ContainsKey(key);

        public string String(string key, string fallback = null) =>
            _values.TryGetValue(key, out string value) ? value : fallback;

        public double Double(string key, double fallback)
        {
            if (!_values.TryGetValue(key, out string raw)) return fallback;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : throw new FormatException($"--{key}: „{raw}” nie jest liczbą");
        }

        public int Int(string key, int fallback) => (int)Math.Round(Double(key, fallback));

        /// <summary>Lista częstotliwości rozdzielona przecinkami, np. <c>--freqs 500,1000,2000</c>.</summary>
        public double[] Doubles(string key, double[] fallback)
        {
            string raw = String(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(part =>
                          double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                              ? v
                              : throw new FormatException($"--{key}: „{part}” nie jest liczbą"))
                      .ToArray();
        }

        public M2Cal.Core.Ear Ear(string key, M2Cal.Core.Ear fallback)
        {
            string raw = String(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            return M2Cal.Core.EarExtensions.TryParse(raw, out M2Cal.Core.Ear ear)
                ? ear
                : throw new FormatException($"--{key}: „{raw}” to nie L / P / LP");
        }
    }
}
