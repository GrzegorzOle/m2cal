using System;

namespace M2Cal.Core
{
    /// <summary>
    /// Synteza bodźców tonalnych. To jest kod DSP, który podlega wzorcowaniu — aplikacja
    /// docelowa musi generować bodźce dokładnie tą samą klasą, inaczej plik kalibracyjny
    /// przestaje opisywać rzeczywisty tor.
    ///
    /// Konwencja poziomu: sinus pełnej skali = 0 dBFS, czyli amplituda szczytowa 1,0.
    /// </summary>
    public sealed class ToneSynthesizer
    {
        /// <summary>Czas narastania i opadania obwiedni (ISO 8253-1 dopuszcza 20–50 ms).</summary>
        public const double RampSeconds = 0.025;

        /// <summary>Czas trwania fazy „ton włączony" w bodźcu pulsowanym.</summary>
        public const double PulseOnSeconds = 0.225;

        /// <summary>Czas trwania przerwy w bodźcu pulsowanym.</summary>
        public const double PulseOffSeconds = 0.225;

        /// <summary>Głębokość modulacji tonu wobbulowanego (±5 % częstotliwości nośnej).</summary>
        public const double WarbleDepth = 0.05;

        /// <summary>Częstotliwość modulacji tonu wobbulowanego.</summary>
        public const double WarbleRateHz = 5.0;

        private readonly int _sampleRate;

        private double _carrierPhase;
        private double _warblePhase;
        private long _sampleIndex;

        public ToneSynthesizer(int sampleRate)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _sampleRate = sampleRate;
        }

        public int SampleRate => _sampleRate;

        /// <summary>Częstotliwość nośna bodźca w Hz.</summary>
        public double FrequencyHz { get; set; } = 1000.0;

        /// <summary>Poziom bodźca w dBFS względem sinusa pełnej skali.</summary>
        public double LevelDbFs { get; set; } = -20.0;

        /// <summary>Ucho, do którego trafia bodziec. Drugi kanał pozostaje cyfrową ciszą.</summary>
        public Ear Ear { get; set; } = Ear.Left;

        /// <summary>Ton pulsowany (bodziec przesiewowy) zamiast ciągłego (bodziec odniesienia).</summary>
        public bool Pulsed { get; set; }

        /// <summary>Ton wobbulowany — stosowany w polu swobodnym, nie przy wzorcowaniu w słuchawkach.</summary>
        public bool Warble { get; set; }

        /// <summary>
        /// Ustawia bodziec od nowa: zeruje fazę, obwiednię i licznik próbek. Wywoływane przy
        /// starcie odtwarzania, żeby każdy bodziec brzmiał identycznie niezależnie od historii.
        /// </summary>
        public void Reset()
        {
            _carrierPhase = 0.0;
            _warblePhase = 0.0;
            _sampleIndex = 0;
        }

        /// <summary>Amplituda szczytowa odpowiadająca danemu poziomowi w dBFS.</summary>
        public static double DbFsToAmplitude(double dbFs) => Math.Pow(10.0, dbFs / 20.0);

        /// <summary>Poziom w dBFS odpowiadający danej amplitudzie szczytowej.</summary>
        public static double AmplitudeToDbFs(double amplitude)
        {
            if (amplitude <= 0.0) return double.NegativeInfinity;
            return 20.0 * Math.Log10(amplitude);
        }

        /// <summary>
        /// Wypełnia bufor przeplatanym stereo (L, P, L, P, ...). Faza jest ciągła między
        /// wywołaniami, więc bufory można podawać dowolnymi porcjami bez trzasków na styku.
        /// </summary>
        public void Render(float[] buffer, int offset, int frameCount)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || frameCount < 0 || offset + frameCount * 2 > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(frameCount));

            double amplitude = DbFsToAmplitude(LevelDbFs);
            Ear.Gains(out float gainL, out float gainR);

            double carrierStep = 2.0 * Math.PI * FrequencyHz / _sampleRate;
            double warbleStep = 2.0 * Math.PI * WarbleRateHz / _sampleRate;

            for (int i = 0; i < frameCount; i++)
            {
                double step = carrierStep;
                if (Warble)
                {
                    step *= 1.0 + WarbleDepth * Math.Sin(_warblePhase);
                    _warblePhase += warbleStep;
                    if (_warblePhase > 2.0 * Math.PI) _warblePhase -= 2.0 * Math.PI;
                }

                double sample = Math.Sin(_carrierPhase) * amplitude * Envelope(_sampleIndex);

                _carrierPhase += step;
                if (_carrierPhase > 2.0 * Math.PI) _carrierPhase -= 2.0 * Math.PI;
                _sampleIndex++;

                int idx = offset + i * 2;
                buffer[idx] = (float)(sample * gainL);
                buffer[idx + 1] = (float)(sample * gainR);
            }
        }

        /// <summary>
        /// Obwiednia bodźca: podniesiony cosinus na narastaniu i opadaniu. Ton ciągły ma tylko
        /// narastanie na starcie; ton pulsowany dostaje pełny cykl on/off z rampami po obu stronach.
        /// </summary>
        private double Envelope(long sampleIndex)
        {
            double t = (double)sampleIndex / _sampleRate;

            if (!Pulsed)
                return RaisedCosine(Math.Min(t / RampSeconds, 1.0));

            double period = PulseOnSeconds + PulseOffSeconds;
            double phase = t % period;

            if (phase >= PulseOnSeconds) return 0.0;
            if (phase < RampSeconds) return RaisedCosine(phase / RampSeconds);

            double untilOff = PulseOnSeconds - phase;
            if (untilOff < RampSeconds) return RaisedCosine(untilOff / RampSeconds);

            return 1.0;
        }

        private static double RaisedCosine(double x)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            return 0.5 * (1.0 - Math.Cos(Math.PI * x));
        }
    }
}
