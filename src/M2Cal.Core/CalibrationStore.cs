using System;
using System.Text.Json;

namespace M2Cal.Core
{
    /// <summary>
    /// Odczyt i zapis pliku kalibracyjnego oraz tabeli RETSPL. Serializacja trzyma się
    /// zwykłych POCO i camelCase — plik ma być czytelny dla człowieka i możliwy do
    /// sparsowania dowolnym parserem JSON, gdyby serializator w aplikacji docelowej zawiódł.
    /// </summary>
    public static class CalibrationStore
    {
        /// <summary>Zalecana nazwa artefaktu przenoszonego do aplikacji docelowej.</summary>
        public const string DefaultFileName = "calibration.m2cal.json";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static string Serialize(CalibrationFile calibration) =>
            JsonSerializer.Serialize(calibration, Options);

        public static CalibrationFile Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("pusty plik kalibracyjny", nameof(json));

            return JsonSerializer.Deserialize<CalibrationFile>(json, Options);
        }

        public static string SerializeRetspl(RetsplTable table) =>
            JsonSerializer.Serialize(table, Options);

        public static RetsplTable DeserializeRetspl(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("pusta tabela RETSPL", nameof(json));

            return JsonSerializer.Deserialize<RetsplTable>(json, Options);
        }
    }
}
