using System;
using System.Collections.Generic;
using System.Linq;
using M2Cal.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace M2Cal.Cli
{
    public sealed class RenderDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int SampleRate { get; set; }
        public int BitDepth { get; set; }
        public int ChannelCount { get; set; }
        public int VolumePercent { get; set; }
        public bool IsDefault { get; set; }

        /// <summary>Rozpoznanie MOTU M2 po nazwie endpointu — do oznaczenia na liście.</summary>
        public bool LooksLikeMotuM2 =>
            !string.IsNullOrEmpty(Name) &&
            Name.IndexOf("M2", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (Name.IndexOf("MOTU", StringComparison.OrdinalIgnoreCase) >= 0 ||
             Name.IndexOf("M Series", StringComparison.OrdinalIgnoreCase) >= 0);

        public DeviceFingerprint ToFingerprint() => new DeviceFingerprint
        {
            DeviceId = Id,
            DeviceName = Name,
            SampleRate = SampleRate,
            BitDepth = BitDepth,
            ChannelCount = ChannelCount,
            EndpointVolumePercent = VolumePercent
        };
    }

    /// <summary>
    /// Warstwa audio CLI: WASAPI w trybie współdzielonym. Świadomie ten sam tryb, w jakim gra
    /// aplikacja docelowa — wzorcowanie ma obejmować także miksik Windows.
    ///
    /// Synteza pracuje w formacie miksu urządzenia, żeby na drodze bodźca nie stanął resampler.
    /// </summary>
    public static class AudioEngine
    {
        public static bool IsSupported => OperatingSystem.IsWindows();

        public static IReadOnlyList<RenderDevice> Enumerate()
        {
            RequireWindows();

            using (var enumerator = new MMDeviceEnumerator())
            {
                string defaultId = null;
                try
                {
                    defaultId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
                }
                catch (Exception)
                {
                    // brak domyślnego urządzenia nie jest błędem krytycznym przy samym listowaniu
                }

                return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                 .Select(d => Describe(d, defaultId))
                                 .ToList();
            }
        }

        /// <summary>
        /// Wybiera urządzenie: po fragmencie nazwy lub identyfikatorze, a bez wskazania —
        /// MOTU M2, jeśli jest dokładnie jedno. Nie zgaduje: przy niejednoznaczności rzuca błąd.
        /// </summary>
        public static RenderDevice Resolve(string wanted)
        {
            var devices = Enumerate();
            if (devices.Count == 0)
                throw new InvalidOperationException("nie znaleziono aktywnego urządzenia odtwarzającego");

            if (!string.IsNullOrWhiteSpace(wanted))
            {
                var matches = devices
                    .Where(d => string.Equals(d.Id, wanted, StringComparison.OrdinalIgnoreCase)
                             || d.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (matches.Count == 1) return matches[0];
                if (matches.Count == 0)
                    throw new InvalidOperationException($"żadne urządzenie nie pasuje do „{wanted}”");

                throw new InvalidOperationException(
                    $"„{wanted}” pasuje do {matches.Count} urządzeń — doprecyzuj --device");
            }

            var motu = devices.Where(d => d.LooksLikeMotuM2).ToList();
            if (motu.Count == 1) return motu[0];

            if (motu.Count > 1)
                throw new InvalidOperationException("znaleziono kilka urządzeń MOTU M2 — wskaż jedno przez --device");

            throw new InvalidOperationException(
                "nie znaleziono MOTU M2. Wskaż urządzenie przez --device (lista: m2cal devices)");
        }

        /// <summary>Odtwarza bodziec przez wskazane urządzenie przez zadany czas.</summary>
        public static void Play(RenderDevice device, ToneSynthesizer synth, TimeSpan duration)
        {
            RequireWindows();

            using (var enumerator = new MMDeviceEnumerator())
            using (var mmDevice = enumerator.GetDevice(device.Id))
            using (var output = new WasapiOut(mmDevice, AudioClientShareMode.Shared, true, 100))
            {
                synth.Reset();
                output.Init(new ToneSampleProvider(synth));
                output.Play();

                var deadline = DateTime.UtcNow + duration;
                while (DateTime.UtcNow < deadline && output.PlaybackState == PlaybackState.Playing)
                    System.Threading.Thread.Sleep(20);

                output.Stop();
            }
        }

        /// <summary>
        /// Uruchamia bodziec i gra go aż do zwolnienia sesji. Używane przy wzorcowaniu, gdzie
        /// ton musi brzmieć nieprzerwanie, dopóki operator odczytuje wskazanie miernika.
        /// </summary>
        public static PlaybackSession Start(RenderDevice device, ToneSynthesizer synth)
        {
            RequireWindows();
            return new PlaybackSession(device, synth);
        }

        private static RenderDevice Describe(MMDevice device, string defaultId)
        {
            var format = device.AudioClient.MixFormat;

            return new RenderDevice
            {
                Id = device.ID,
                Name = device.FriendlyName,
                SampleRate = format.SampleRate,
                BitDepth = format.BitsPerSample,
                ChannelCount = format.Channels,
                VolumePercent = (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100),
                IsDefault = string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase)
            };
        }

        private static void RequireWindows()
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException(
                    "komendy audio wymagają Windows (WASAPI). Bez sprzętu działa: m2cal selftest");
        }
    }

    /// <summary>Trwające odtwarzanie bodźca; zatrzymuje się przy zwolnieniu obiektu.</summary>
    public sealed class PlaybackSession : IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator;
        private readonly MMDevice _device;
        private readonly WasapiOut _output;

        internal PlaybackSession(RenderDevice device, ToneSynthesizer synth)
        {
            _enumerator = new MMDeviceEnumerator();
            _device = _enumerator.GetDevice(device.Id);
            _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, 100);

            synth.Reset();
            _output.Init(new ToneSampleProvider(synth));
            _output.Play();
        }

        public void Dispose()
        {
            try { _output.Stop(); } catch (Exception) { /* zamykanie nie może maskować wyniku pomiaru */ }
            _output.Dispose();
            _device.Dispose();
            _enumerator.Dispose();
        }
    }

    /// <summary>Most między <see cref="ToneSynthesizer"/> a łańcuchem NAudio.</summary>
    internal sealed class ToneSampleProvider : ISampleProvider
    {
        private readonly ToneSynthesizer _synth;

        public ToneSampleProvider(ToneSynthesizer synth)
        {
            _synth = synth;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(synth.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            _synth.Render(buffer, offset, frames);
            return frames * 2;
        }
    }
}
