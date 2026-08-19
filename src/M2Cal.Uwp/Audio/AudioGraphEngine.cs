using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using M2Cal.Core;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Devices;
using Windows.Media.Render;

namespace M2Cal.Uwp.Audio
{
    public sealed class RenderDeviceInfo
    {
        public DeviceInformation Device { get; set; }

        public string Id => Device?.Id;

        public string Name => Device?.Name;

        /// <summary>Rozpoznanie MOTU M2 po nazwie endpointu.</summary>
        public bool LooksLikeMotuM2 =>
            !string.IsNullOrEmpty(Name) &&
            Name.IndexOf("M2", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (Name.IndexOf("MOTU", StringComparison.OrdinalIgnoreCase) >= 0 ||
             Name.IndexOf("M Series", StringComparison.OrdinalIgnoreCase) >= 0);

        public override string ToString() => (LooksLikeMotuM2 ? "* " : "   ") + Name;
    }

    /// <summary>
    /// Odtwarzanie bodźca przez AudioGraph. To jest ten sam tor, którym gra aplikacja
    /// docelowa (również UWP), więc wzorcowanie obejmuje dokładnie tę drogę sygnału.
    ///
    /// Ograniczenie, którego nie da się obejść w kontenerze aplikacji: UWP nie odczyta
    /// głośności endpointu ani ustawień miksera Windows. Ten stan potwierdza operator
    /// i to potwierdzenie ląduje w odcisku urządzenia.
    /// </summary>
    public sealed class AudioGraphEngine : IDisposable
    {
        private AudioGraph _graph;
        private AudioDeviceOutputNode _output;
        private AudioFrameInputNode _input;
        private ToneSynthesizer _synth;
        private float[] _scratch;

        public bool IsPlaying { get; private set; }

        public int SampleRate => _graph == null ? 0 : (int)_graph.EncodingProperties.SampleRate;

        public int ChannelCount => _graph == null ? 0 : (int)_graph.EncodingProperties.ChannelCount;

        public int BitDepth => _graph == null ? 0 : (int)_graph.EncodingProperties.BitsPerSample;

        public RenderDeviceInfo Device { get; private set; }

        public static async Task<IReadOnlyList<RenderDeviceInfo>> ListDevicesAsync()
        {
            var devices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());

            return devices
                .Select(d => new RenderDeviceInfo { Device = d })
                .OrderByDescending(d => d.LooksLikeMotuM2)
                .ThenBy(d => d.Name)
                .ToList();
        }

        /// <summary>Buduje graf na wskazanym urządzeniu. Poprzedni graf jest zamykany.</summary>
        public async Task OpenAsync(RenderDeviceInfo device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));

            Close();

            var settings = new AudioGraphSettings(AudioRenderCategory.Media)
            {
                PrimaryRenderDevice = device.Device,
                QuantumSizeSelectionMode = QuantumSizeSelectionMode.SystemDefault
            };

            var graphResult = await AudioGraph.CreateAsync(settings);
            if (graphResult.Status != AudioGraphCreationStatus.Success)
                throw new InvalidOperationException("nie udało się utworzyć grafu audio: " + graphResult.Status);

            _graph = graphResult.Graph;

            var outputResult = await _graph.CreateDeviceOutputNodeAsync();
            if (outputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                Close();
                throw new InvalidOperationException("nie udało się otworzyć wyjścia audio: " + outputResult.Status);
            }

            _output = outputResult.DeviceOutputNode;

            // Węzeł wejściowy pracuje w formacie grafu — na drodze bodźca nie staje resampler.
            var properties = _graph.EncodingProperties;
            properties.ChannelCount = 2;

            _input = _graph.CreateFrameInputNode(properties);
            _input.AddOutgoingConnection(_output);
            _input.QuantumStarted += OnQuantumStarted;
            _input.Stop();

            _graph.Start();
            Device = device;
        }

        /// <summary>Startuje bodziec. Kolejne wywołania podmieniają parametry bez przerywania grafu.</summary>
        public void Play(ToneSynthesizer synth)
        {
            if (_graph == null) throw new InvalidOperationException("graf audio nie jest otwarty");

            synth.Reset();
            _synth = synth;
            IsPlaying = true;
            _input.Start();
        }

        public void Stop()
        {
            if (_input == null) return;

            _input.Stop();
            IsPlaying = false;
            _synth = null;
        }

        /// <summary>
        /// Odcisk toru zapisywany w pliku kalibracyjnym. Głośność endpointu pochodzi
        /// z potwierdzenia operatora — UWP nie ma do niej dostępu programowego.
        /// </summary>
        public DeviceFingerprint Fingerprint(bool endpointVolumeConfirmed) => new DeviceFingerprint
        {
            DeviceId = Device?.Id,
            DeviceName = Device?.Name,
            SampleRate = SampleRate,
            BitDepth = BitDepth,
            ChannelCount = ChannelCount,
            EndpointVolumePercent = endpointVolumeConfirmed ? 100 : -1
        };

        private void OnQuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
        {
            var synth = _synth;
            if (synth == null || args.RequiredSamples <= 0) return;

            sender.AddFrame(RenderFrame(synth, (uint)args.RequiredSamples));
        }

        private unsafe AudioFrame RenderFrame(ToneSynthesizer synth, uint frameCount)
        {
            const int channels = 2;
            int sampleCount = (int)frameCount * channels;

            // Bufor pośredni jest utrzymywany między kwantami — alokacja w callbacku audio
            // oznaczałaby pracę GC na ścieżce czasu rzeczywistego i ryzyko przerw w bodźcu.
            if (_scratch == null || _scratch.Length < sampleCount)
                _scratch = new float[sampleCount];

            synth.Render(_scratch, 0, (int)frameCount);

            var frame = new AudioFrame((uint)(sampleCount * sizeof(float)));

            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* raw, out _);
                Marshal.Copy(_scratch, 0, (IntPtr)raw, sampleCount);
            }

            return frame;
        }

        private void Close()
        {
            if (_input != null)
            {
                _input.QuantumStarted -= OnQuantumStarted;
                _input.Dispose();
                _input = null;
            }

            _output?.Dispose();
            _output = null;

            if (_graph != null)
            {
                _graph.Stop();
                _graph.Dispose();
                _graph = null;
            }

            IsPlaying = false;
            _synth = null;
            Device = null;
        }

        public void Dispose() => Close();
    }
}
