using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using M2Cal.Core;
using M2Cal.Uwp.Audio;
using M2Cal.Uwp.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace M2Cal.Uwp
{
    /// <summary>
    /// Ekran wzorcowania: częstotliwość, poziom cyfrowy, kanał, odczyt z miernika — jeden
    /// wiersz mapy na pomiar. Mapa jest jedynym artefaktem, z którego korzysta aplikacja
    /// docelowa, więc GUI nie przelicza niczego „po drodze": zapisuje surowe obserwacje.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly AudioGraphEngine _engine = new AudioGraphEngine();
        private readonly ObservableCollection<CalibrationPointRow> _rows = new ObservableCollection<CalibrationPointRow>();
        private readonly CalibrationFile _calibration = new CalibrationFile();

        private IReadOnlyList<RenderDeviceInfo> _devices = new List<RenderDeviceInfo>();
        private ToneSynthesizer _liveSynth;
        private bool _ready;
        private bool _suppressInvalidate;

        /// <summary>Dolna granica regulacji. Poniżej tego poziomu bodziec ginie w szumie toru.</summary>
        private const double MinLevelDbFs = -120.0;

        public MainPage()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                App.Log("MainPage.InitializeComponent", ex);
                throw;
            }

            PointsList.ItemsSource = _rows;
            _rows.CollectionChanged += (s, e) => UpdateStatus();

            Loaded += OnLoaded;
            Unloaded += (s, e) => _engine.Dispose();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ready = true;
            await RefreshDevicesAsync();
            UpdateStatus();
        }

        // ---------------------------------------------------------------- urządzenia

        private async void OnRefreshDevices(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();

        private async System.Threading.Tasks.Task RefreshDevicesAsync()
        {
            try
            {
                _devices = await AudioGraphEngine.ListDevicesAsync();
                DeviceBox.ItemsSource = _devices.Select(d => d.ToString()).ToList();

                int motuIndex = _devices.ToList().FindIndex(d => d.LooksLikeMotuM2);
                if (motuIndex >= 0) DeviceBox.SelectedIndex = motuIndex;

                if (motuIndex < 0)
                    DeviceStatus.Text = "nie wykryto MOTU M2 — wybierz urządzenie ręcznie";
            }
            catch (Exception ex)
            {
                DeviceStatus.Text = "nie udało się odczytać listy urządzeń: " + ex.Message;
            }
        }

        private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;

            _engine.Stop();
            SetPlaybackButtons(playing: false, deviceOpen: false);
            DeviceStatus.Text = "tor nieotwarty — kliknij „Otwórz tor”";
        }

        private async void OnOpenDevice(object sender, RoutedEventArgs e)
        {
            var device = SelectedDevice();
            if (device == null)
            {
                DeviceStatus.Text = "najpierw wybierz urządzenie";
                return;
            }

            try
            {
                await _engine.OpenAsync(device);

                DeviceStatus.Text = $"{device.Name}\n" +
                                    $"{_engine.SampleRate} Hz / {_engine.BitDepth} bit / {_engine.ChannelCount} kan." +
                                    (device.LooksLikeMotuM2 ? "" : "\n! to nie jest MOTU M2 — wzorcowanie dotyczy innego toru");

                SetPlaybackButtons(playing: false, deviceOpen: true);
            }
            catch (Exception ex)
            {
                DeviceStatus.Text = "nie udało się otworzyć toru: " + ex.Message;
                SetPlaybackButtons(playing: false, deviceOpen: false);
            }

            UpdateStatus();
        }

        private RenderDeviceInfo SelectedDevice()
        {
            int index = DeviceBox.SelectedIndex;
            return index >= 0 && index < _devices.Count ? _devices[index] : null;
        }

        // ---------------------------------------------------------------- bodziec

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            if (!TryReadStimulus(out double frequency, out double level, out string error))
            {
                DeviceStatus.Text = error;
                return;
            }

            try
            {
                _liveSynth = new ToneSynthesizer(_engine.SampleRate)
                {
                    FrequencyHz = frequency,
                    LevelDbFs = level,
                    Ear = SelectedEar(),
                    Pulsed = PulsedBox.IsChecked == true,
                    Warble = WarbleBox.IsChecked == true
                };

                _engine.Play(_liveSynth);
                SetPlaybackButtons(playing: true, deviceOpen: true);
            }
            catch (Exception ex)
            {
                DeviceStatus.Text = "nie udało się uruchomić bodźca: " + ex.Message;
            }
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            _engine.Stop();
            _liveSynth = null;
            SetPlaybackButtons(playing: false, deviceOpen: true);
        }

        /// <summary>
        /// Krokowa regulacja poziomu. Poziom wolno zmieniać w trakcie grania: pole i generator
        /// pozostają zgodne, więc wskazanie miernika nadal odpowiada temu, co operator zapisze.
        /// Pozostałe parametry bodźca przerywają ton, bo zmieniają jego charakter, nie amplitudę.
        /// </summary>
        private void OnLevelStep(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement source) || !TryParse(source.Tag as string, out double step))
                return;

            if (!TryParse(LevelBox.Text, out double level)) level = 0.0;

            double next = Math.Round(Math.Min(0.0, Math.Max(MinLevelDbFs, level + step)), 1);

            _suppressInvalidate = true;
            LevelBox.Text = next.ToString("0.#", CultureInfo.InvariantCulture);
            _suppressInvalidate = false;

            if (_liveSynth != null) _liveSynth.LevelDbFs = next;

            UpdateSensitivityPreview();
        }

        /// <summary>
        /// Zmiana parametrów w trakcie odtwarzania zatrzymuje bodziec. Świadomie: wskazanie
        /// miernika ma odpowiadać dokładnie temu bodźcowi, który operator zaraz zapisze.
        /// </summary>
        private void OnStimulusTextChanged(object sender, TextChangedEventArgs e) => InvalidateStimulus();

        private void OnStimulusToggled(object sender, RoutedEventArgs e) => InvalidateStimulus();

        private void InvalidateStimulus()
        {
            if (!_ready || _suppressInvalidate) return;

            if (_engine.IsPlaying)
            {
                _engine.Stop();
                _liveSynth = null;
                SetPlaybackButtons(playing: false, deviceOpen: _engine.SampleRate > 0);
            }

            UpdateSensitivityPreview();
        }

        private void OnMeasuredChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            UpdateSensitivityPreview();
        }

        /// <summary>
        /// Przycisk zapisu punktu bywa nieaktywny i bez wyjaśnienia wygląda jak awaria.
        /// Podpowiedź mówi wprost, czego brakuje, zamiast zostawiać operatora z szarym przyciskiem.
        /// </summary>
        private void UpdateSensitivityPreview()
        {
            bool hasStimulus = TryReadStimulus(out _, out double level, out string stimulusError);
            bool hasMeasurement = TryParse(MeasuredSplBox.Text, out double measured);

            AddPointButton.IsEnabled = hasStimulus && hasMeasurement;

            if (hasStimulus && hasMeasurement)
            {
                SensitivityPreview.Text =
                    $"czułość toru: {measured - level:0.0} dB SPL przy sinusie pełnej skali";
            }
            else if (!hasStimulus)
            {
                SensitivityPreview.Text = stimulusError ?? "uzupełnij częstotliwość i poziom bodźca";
            }
            else
            {
                SensitivityPreview.Text =
                    "wpisz odczyt z miernika w dB SPL — wtedy będzie można zapisać punkt";
            }
        }

        private Ear SelectedEar() => EarRight.IsChecked == true ? Ear.Right : Ear.Left;

        private bool TryReadStimulus(out double frequency, out double level, out string error)
        {
            level = 0;
            error = null;

            if (!TryParse(FrequencyBox.Text, out frequency) || frequency <= 0)
            {
                error = "częstotliwość musi być liczbą dodatnią";
                return false;
            }

            if (!TryParse(LevelBox.Text, out level))
            {
                error = "poziom sygnału musi być liczbą w dBFS";
                return false;
            }

            if (level > 0)
            {
                error = "poziom powyżej 0 dBFS przesterowuje tor";
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- mapa pomiarów

        private void OnAddPoint(object sender, RoutedEventArgs e)
        {
            if (!TryReadStimulus(out double frequency, out double level, out string error))
            {
                UpdateStatus(error);
                return;
            }

            if (!TryParse(MeasuredSplBox.Text, out double measured))
            {
                UpdateStatus("wpisz odczyt z miernika w dB SPL");
                return;
            }

            var point = new CalibrationPoint
            {
                FrequencyHz = frequency,
                Ear = SelectedEar().ToCode(),
                StimulusDbFs = level,
                MeasuredSpl = measured,
                MeasuredAtUtc = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text.Trim()
            };

            _calibration.Points.Add(point);
            _rows.Add(new CalibrationPointRow(point));

            MeasuredSplBox.Text = string.Empty;
            NoteBox.Text = string.Empty;
        }

        private void OnRemovePoint(object sender, RoutedEventArgs e)
        {
            if (!(PointsList.SelectedItem is CalibrationPointRow row)) return;

            _calibration.Points.Remove(row.Point);
            _rows.Remove(row);
        }

        // ---------------------------------------------------------------- plik

        private async void OnSaveFile(object sender, RoutedEventArgs e)
        {
            if (_calibration.Points.Count == 0)
            {
                UpdateStatus("mapa jest pusta — nie ma czego zapisać");
                return;
            }

            if (_engine.SampleRate == 0)
            {
                UpdateStatus("otwórz tor przed zapisem — bez odcisku urządzenia plik jest bezużyteczny");
                return;
            }

            // Cała ścieżka zapisu jest osłonięta, łącznie z otwarciem okna wyboru pliku.
            // Wyjątek w `async void` kończy proces, a razem z nim przepada cała sesja pomiarowa.
            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = "calibration.m2cal"
                };
                picker.FileTypeChoices.Add("Plik kalibracyjny m2cal", new List<string> { ".json" });

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                _calibration.CreatedAtUtc = DateTime.UtcNow;
                _calibration.Operator = NullIfBlank(OperatorBox.Text);
                _calibration.Transducer = NullIfBlank(TransducerBox.Text);
                _calibration.Coupler = NullIfBlank(CouplerBox.Text);
                _calibration.Notes = NullIfBlank(NotesBox.Text);
                _calibration.Device = _engine.Fingerprint(VolumeConfirmedBox.IsChecked == true);

                await FileIO.WriteTextAsync(file, CalibrationStore.Serialize(_calibration));
                UpdateStatus($"zapisano {_calibration.Points.Count} punktów do {file.Name}");
            }
            catch (Exception ex)
            {
                App.Log("OnSaveFile", ex);
                UpdateStatus($"nie udało się zapisać pliku: {ex.GetType().Name} — {ex.Message}");
            }
        }

        private async void OnLoadFile(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
                picker.FileTypeFilter.Add(".json");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file == null) return;

                var loaded = CalibrationStore.Deserialize(await FileIO.ReadTextAsync(file));

                _calibration.Points.Clear();
                _rows.Clear();

                foreach (var point in loaded.Points ?? new List<CalibrationPoint>())
                {
                    _calibration.Points.Add(point);
                    _rows.Add(new CalibrationPointRow(point));
                }

                _calibration.Verify = loaded.Verify;
                _calibration.RefDbFs = loaded.RefDbFs;

                OperatorBox.Text = loaded.Operator ?? string.Empty;
                TransducerBox.Text = loaded.Transducer ?? string.Empty;
                CouplerBox.Text = loaded.Coupler ?? string.Empty;
                NotesBox.Text = loaded.Notes ?? string.Empty;

                UpdateStatus($"wczytano {_rows.Count} punktów z {file.Name}");
            }
            catch (Exception ex)
            {
                App.Log("OnLoadFile", ex);
                UpdateStatus($"nie udało się wczytać pliku: {ex.GetType().Name} — {ex.Message}");
            }
        }

        // ---------------------------------------------------------------- stan

        private void OnVolumeConfirmationChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            UpdateStatus();
        }

        /// <summary>
        /// Uruchamia ten sam zestaw testów, co <c>m2cal selftest</c>. W GUI ma to osobną
        /// wartość: kompilacja Release idzie przez .NET Native, gdzie serializacja pliku
        /// kalibracyjnego może zachować się inaczej niż w Debug — test sprawdza to na
        /// maszynie, na której faktycznie prowadzi się wzorcowanie.
        /// </summary>
        private async void OnSelfTest(object sender, RoutedEventArgs e)
        {
            var report = SelfTest.Run();

            var lines = report.Cases
                .Select(c => $"[{(c.Passed ? "OK" : "BŁĄD")}] {c.Name}" +
                             (string.IsNullOrEmpty(c.Detail) ? "" : "  — " + c.Detail));

            var dialog = new ContentDialog
            {
                Title = report.AllPassed
                    ? $"Selftest: {report.PassedCount}/{report.Cases.Count} OK"
                    : $"Selftest: {report.Cases.Count - report.PassedCount} testów nie przeszło",
                CloseButtonText = "Zamknij",
                Content = new ScrollViewer
                {
                    MaxHeight = 420,
                    Content = new TextBlock
                    {
                        Text = string.Join("\n", lines),
                        FontFamily = new Windows.UI.Xaml.Media.FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            await dialog.ShowAsync();
        }

        private void SetPlaybackButtons(bool playing, bool deviceOpen)
        {
            PlayButton.IsEnabled = deviceOpen && !playing;
            StopButton.IsEnabled = deviceOpen && playing;
        }

        /// <summary>
        /// Pasek stanu mówi wprost, czego brakuje do dopuszczenia. Sama mapa nie wystarcza:
        /// bez potwierdzonej głośności endpointu i bez kontroli verify bramka odrzuci plik.
        /// </summary>
        private void UpdateStatus(string message = null)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(message)) parts.Add(message);

            parts.Add(_rows.Count == 0
                ? "mapa jest pusta"
                : $"punktów w mapie: {_rows.Count}");

            var missing = new List<string>();
            if (VolumeConfirmedBox.IsChecked != true) missing.Add("potwierdzenia głośności endpointu");
            if (_calibration.Verify == null || !_calibration.Verify.Passed) missing.Add("pozytywnej kontroli verify");
            if (string.IsNullOrWhiteSpace(TransducerBox.Text)) missing.Add("modelu przetwornika");

            if (missing.Count > 0)
                parts.Add("do dopuszczenia brakuje: " + string.Join(", ", missing));

            var spread = LargestSensitivitySpread();
            if (spread > 2.0)
                parts.Add($"rozrzut czułości między powtórzeniami sięga {spread:0.0} dB — sprawdź osadzenie słuchawek");

            StatusText.Text = string.Join("   •   ", parts);
        }

        private double LargestSensitivitySpread()
        {
            double largest = 0.0;

            foreach (var group in _calibration.Points.GroupBy(p => new { p.FrequencyHz, p.Ear }))
            {
                double spread = group.Max(p => p.SplAtFullScale) - group.Min(p => p.SplAtFullScale);
                if (spread > largest) largest = spread;
            }

            return largest;
        }

        // ---------------------------------------------------------------- pomocnicze

        /// <summary>Przyjmuje przecinek i kropkę — operator wpisuje to, co widzi na mierniku.</summary>
        private static bool TryParse(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, out value);
        }

        private static string NullIfBlank(string text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
