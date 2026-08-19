using M2Cal.Core;

namespace M2Cal.Uwp.ViewModels
{
    /// <summary>
    /// Wiersz mapy pomiarów pokazywany w GUI. Trzyma referencję do punktu w pliku
    /// kalibracyjnym, więc lista i zapisywany artefakt nie mogą się rozjechać.
    /// </summary>
    public sealed class CalibrationPointRow
    {
        public CalibrationPointRow(CalibrationPoint point)
        {
            Point = point;
        }

        public CalibrationPoint Point { get; }

        public string Frequency => $"{Point.FrequencyHz:0} Hz";

        public string Ear => Point.Ear;

        public string Stimulus => $"{Point.StimulusDbFs:0.0} dBFS";

        public string Measured => $"{Point.MeasuredSpl:0.0} dB SPL";

        /// <summary>Czułość toru wynikająca z tego punktu — poziom akustyczny sinusa pełnej skali.</summary>
        public string Sensitivity => $"{Point.SplAtFullScale:0.0} dB SPL @ 0 dBFS";
    }
}
