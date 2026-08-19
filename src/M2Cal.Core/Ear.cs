namespace M2Cal.Core
{
    /// <summary>Ucho / kanał wyjściowy toru. Wzorcowanie prowadzi się osobno dla każdego ucha.</summary>
    public enum Ear
    {
        Left = 0,
        Right = 1,
        Both = 2
    }

    public static class EarExtensions
    {
        /// <summary>Krótki kod używany w pliku kalibracyjnym i w CLI: L / P / LP.</summary>
        public static string ToCode(this Ear ear)
        {
            switch (ear)
            {
                case Ear.Left: return "L";
                case Ear.Right: return "P";
                default: return "LP";
            }
        }

        public static bool TryParse(string text, out Ear ear)
        {
            ear = Ear.Left;
            if (string.IsNullOrWhiteSpace(text)) return false;

            switch (text.Trim().ToUpperInvariant())
            {
                case "L":
                case "LEFT":
                case "LEWE":
                    ear = Ear.Left;
                    return true;
                case "P":
                case "R":
                case "RIGHT":
                case "PRAWE":
                    ear = Ear.Right;
                    return true;
                case "LP":
                case "BOTH":
                case "OBA":
                    ear = Ear.Both;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Mnożnik amplitudy dla lewej/prawej próbki w ramce stereo.</summary>
        public static void Gains(this Ear ear, out float left, out float right)
        {
            left = ear == Ear.Right ? 0f : 1f;
            right = ear == Ear.Left ? 0f : 1f;
        }
    }
}
