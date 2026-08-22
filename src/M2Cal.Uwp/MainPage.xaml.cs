using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using M2Cal.Core;
using M2Cal.Uwp.Audio;
using M2Cal.Uwp.ViewModels;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

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

        /// <summary>Adres pliku .appinstaller — ten sam, który generuje workflow wydania.</summary>
        private const string AppInstallerUri =
            "https://github.com/GrzegorzOle/m2cal/releases/latest/download/m2cal.appinstaller";

        private IReadOnlyList<RenderDeviceInfo> _devices = new List<RenderDeviceInfo>();
        private ToneSynthesizer _liveSynth;
        private bool _ready;
        private bool _suppressInvalidate;

        /// <summary>Czy w mapie są pomiary, których nie zapisano do pliku.</summary>
        private bool _dirty;

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

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;

            Loaded += OnLoaded;
            Unloaded += (s, e) => _engine.Dispose();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ready = true;
            FullScreenButton.Content = ApplicationView.GetForCurrentView().IsFullScreenMode
                ? "Tryb okna"
                : "Pełny ekran";

            await RefreshDevicesAsync();
            UpdateStatus();
            await CheckForUpdateAsync();
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
            _dirty = true;

            MeasuredSplBox.Text = string.Empty;
            NoteBox.Text = string.Empty;
        }

        private void OnRemovePoint(object sender, RoutedEventArgs e)
        {
            if (!(PointsList.SelectedItem is CalibrationPointRow row)) return;

            _calibration.Points.Remove(row.Point);
            _rows.Remove(row);
            _dirty = true;
        }

        // ---------------------------------------------------------------- metadane stanowiska

        /// <summary>
        /// Przepisuje formularz do modelu pliku. Wszystko, co nie wynika z pomiaru ani z kodu,
        /// musi pochodzić stąd — narzędzie nie podstawia żadnych wartości normatywnych samo.
        /// </summary>
        private void BuildMetadata()
        {
            _calibration.Operator = NullIfBlank(OperatorBox.Text);
            _calibration.Notes = NullIfBlank(NotesBox.Text);

            // Pola tekstowe zostają dla zgodności wstecznej i dla czytelności pliku.
            _calibration.Transducer = NullIfBlank(TransducerBox.Text);
            _calibration.Coupler = NullIfBlank(CouplerBox.Text);

            _calibration.TransducerDetails = new TransducerInfo
            {
                Model = NullIfBlank(TransducerBox.Text),
                SerialNumber = NullIfBlank(TransducerSerialBox.Text),
                CushionType = SelectedText(CushionBox)
            };

            _calibration.Equipment = new MeasurementChain
            {
                SoundLevelMeter = new InstrumentInfo
                {
                    Manufacturer = NullIfBlank(MeterManufacturerBox.Text),
                    Model = NullIfBlank(MeterModelBox.Text),
                    SerialNumber = NullIfBlank(MeterSerialBox.Text),
                    ConformsToStandard = NullIfBlank(MeterStandardBox.Text),
                    CalibrationCertificate = NullIfBlank(MeterCertificateBox.Text),
                    CalibratedOnUtc = MeterCalibratedOnPicker.Date?.UtcDateTime
                },
                Microphone = new InstrumentInfo
                {
                    Model = NullIfBlank(MicModelBox.Text),
                    SerialNumber = NullIfBlank(MicSerialBox.Text)
                },
                Coupler = new InstrumentInfo
                {
                    Model = NullIfBlank(CouplerBox.Text),
                    SerialNumber = NullIfBlank(CouplerSerialBox.Text)
                },
                CouplerStandard = NullIfBlank(CouplerStandardBox.Text),
                FrequencyWeighting = SelectedText(WeightingBox),
                TimeWeighting = SelectedText(TimeWeightingBox),
                MeasurementMode = SelectedText(MeasurementModeBox),
                CalibratorCheck = new AcousticCalibratorCheck
                {
                    Calibrator = new InstrumentInfo
                    {
                        Model = NullIfBlank(CalibratorModelBox.Text),
                        SerialNumber = NullIfBlank(CalibratorSerialBox.Text)
                    },
                    NominalLevelDbSpl = ParseOptional(CalibratorLevelBox.Text),
                    NominalFrequencyHz = ParseOptional(CalibratorFrequencyBox.Text),
                    ReadingBeforeSessionDbSpl = ParseOptional(CalibratorBeforeBox.Text),
                    ReadingAfterSessionDbSpl = ParseOptional(CalibratorAfterBox.Text)
                }
            };

            // Parametry bodzca czytane z kodu syntezy, a nie przepisywane recznie — dzieki temu
            // plik nie moze opisywac innego bodzca niz ten, ktory faktycznie zabrzmial.
            var wzorzec = new ToneSynthesizer(_engine.SampleRate > 0 ? _engine.SampleRate : 48000)
            {
                Pulsed = PulsedBox.IsChecked == true,
                Warble = WarbleBox.IsChecked == true
            };

            _calibration.Stimulus = StimulusSettings.FromSynthesizer(wzorzec);
            _calibration.Stimulus.TimingSource = NullIfBlank(StdTimingBox.Text);
            _calibration.SynthesizerVersion = ToneSynthesizer.Version;

            _calibration.Ambient = new AmbientConditions
            {
                BackgroundNoiseDbA = ParseOptional(BackgroundNoiseBox.Text),
                TemperatureCelsius = ParseOptional(TemperatureBox.Text),
                RelativeHumidityPercent = ParseOptional(HumidityBox.Text),
                Location = NullIfBlank(LocationBox.Text)
            };

            _calibration.Standards = new StandardsReferences
            {
                Retspl = NullIfBlank(StdRetsplBox.Text),
                LevelTolerance = NullIfBlank(StdToleranceBox.Text),
                StimulusTiming = NullIfBlank(StdTimingBox.Text),
                SoundLevelMeter = NullIfBlank(StdMeterBox.Text),
                Coupler = NullIfBlank(StdCouplerBox.Text),
                AmbientNoise = NullIfBlank(StdAmbientBox.Text)
            };
        }

        /// <summary>Wypełnia formularz danymi z wczytanego pliku.</summary>
        private void ApplyMetadata(CalibrationFile loaded)
        {
            OperatorBox.Text = loaded.Operator ?? string.Empty;
            NotesBox.Text = loaded.Notes ?? string.Empty;

            var transducer = loaded.TransducerDetails;
            TransducerBox.Text = transducer?.Model ?? loaded.Transducer ?? string.Empty;
            TransducerSerialBox.Text = transducer?.SerialNumber ?? string.Empty;
            SelectText(CushionBox, transducer?.CushionType);

            var chain = loaded.Equipment;
            MeterManufacturerBox.Text = chain?.SoundLevelMeter?.Manufacturer ?? string.Empty;
            MeterModelBox.Text = chain?.SoundLevelMeter?.Model ?? string.Empty;
            MeterSerialBox.Text = chain?.SoundLevelMeter?.SerialNumber ?? string.Empty;
            MeterStandardBox.Text = chain?.SoundLevelMeter?.ConformsToStandard ?? string.Empty;
            MeterCertificateBox.Text = chain?.SoundLevelMeter?.CalibrationCertificate ?? string.Empty;
            MeterCalibratedOnPicker.Date = chain?.SoundLevelMeter?.CalibratedOnUtc.HasValue == true
                ? new DateTimeOffset(chain.SoundLevelMeter.CalibratedOnUtc.Value)
                : (DateTimeOffset?)null;

            MicModelBox.Text = chain?.Microphone?.Model ?? string.Empty;
            MicSerialBox.Text = chain?.Microphone?.SerialNumber ?? string.Empty;

            CouplerBox.Text = chain?.Coupler?.Model ?? loaded.Coupler ?? string.Empty;
            CouplerSerialBox.Text = chain?.Coupler?.SerialNumber ?? string.Empty;
            CouplerStandardBox.Text = chain?.CouplerStandard ?? string.Empty;

            SelectText(WeightingBox, chain?.FrequencyWeighting);
            SelectText(TimeWeightingBox, chain?.TimeWeighting);
            SelectText(MeasurementModeBox, chain?.MeasurementMode);

            var calibrator = chain?.CalibratorCheck;
            CalibratorModelBox.Text = calibrator?.Calibrator?.Model ?? string.Empty;
            CalibratorSerialBox.Text = calibrator?.Calibrator?.SerialNumber ?? string.Empty;
            CalibratorLevelBox.Text = Format(calibrator?.NominalLevelDbSpl);
            CalibratorFrequencyBox.Text = Format(calibrator?.NominalFrequencyHz);
            CalibratorBeforeBox.Text = Format(calibrator?.ReadingBeforeSessionDbSpl);
            CalibratorAfterBox.Text = Format(calibrator?.ReadingAfterSessionDbSpl);

            BackgroundNoiseBox.Text = Format(loaded.Ambient?.BackgroundNoiseDbA);
            TemperatureBox.Text = Format(loaded.Ambient?.TemperatureCelsius);
            HumidityBox.Text = Format(loaded.Ambient?.RelativeHumidityPercent);
            LocationBox.Text = loaded.Ambient?.Location ?? string.Empty;

            StdRetsplBox.Text = loaded.Standards?.Retspl ?? string.Empty;
            StdToleranceBox.Text = loaded.Standards?.LevelTolerance ?? string.Empty;
            StdTimingBox.Text = loaded.Standards?.StimulusTiming ?? loaded.Stimulus?.TimingSource ?? string.Empty;
            StdMeterBox.Text = loaded.Standards?.SoundLevelMeter ?? string.Empty;
            StdCouplerBox.Text = loaded.Standards?.Coupler ?? string.Empty;
            StdAmbientBox.Text = loaded.Standards?.AmbientNoise ?? string.Empty;
        }

        private static string SelectedText(ComboBox box) =>
            (box.SelectedItem as ComboBoxItem)?.Content as string;

        private static void SelectText(ComboBox box, string value)
        {
            box.SelectedItem = null;
            if (string.IsNullOrWhiteSpace(value)) return;

            foreach (var item in box.Items)
            {
                if (item is ComboBoxItem entry &&
                    string.Equals(entry.Content as string, value, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedItem = entry;
                    return;
                }
            }
        }

        private static double? ParseOptional(string text) =>
            TryParse(text, out double value) ? value : (double?)null;

        private static string Format(double? value) =>
            value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;

        // ---------------------------------------------------------------- plik

        private async void OnSaveFile(object sender, RoutedEventArgs e) => await SaveToFileAsync();

        /// <summary>Zwraca true, gdy plik faktycznie zapisano — decyduje o tym, czy wolno zamknąć okno.</summary>
        private async Task<bool> SaveToFileAsync()
        {
            if (_calibration.Points.Count == 0)
            {
                UpdateStatus("mapa jest pusta — nie ma czego zapisać");
                return false;
            }

            if (_engine.SampleRate == 0)
            {
                UpdateStatus("otwórz tor przed zapisem — bez odcisku urządzenia plik jest bezużyteczny");
                return false;
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
                if (file == null) return false;

                _calibration.CreatedAtUtc = DateTime.UtcNow;
                _calibration.Device = _engine.Fingerprint(VolumeConfirmedBox.IsChecked == true);
                BuildMetadata();

                await FileIO.WriteTextAsync(file, CalibrationStore.Serialize(_calibration));

                _dirty = false;
                UpdateStatus($"zapisano {_calibration.Points.Count} punktów do {file.Name}");
                return true;
            }
            catch (Exception ex)
            {
                App.Log("OnSaveFile", ex);
                UpdateStatus($"nie udało się zapisać pliku: {ex.GetType().Name} — {ex.Message}");
                return false;
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

                ApplyMetadata(loaded);
                _dirty = false;

                UpdateStatus($"wczytano {_rows.Count} punktów z {file.Name}");
            }
            catch (Exception ex)
            {
                App.Log("OnLoadFile", ex);
                UpdateStatus($"nie udało się wczytać pliku: {ex.GetType().Name} — {ex.Message}");
            }
        }

        // ---------------------------------------------------------------- okno, zamykanie, aktualizacja

        /// <summary>Przełącza pełny ekran i tryb okna — przy pracy zdalnej pasek tytułu bywa niewidoczny.</summary>
        private void OnToggleFullScreen(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();

            if (view.IsFullScreenMode)
            {
                view.ExitFullScreenMode();
                FullScreenButton.Content = "Pełny ekran";
            }
            else if (view.TryEnterFullScreenMode())
            {
                FullScreenButton.Content = "Tryb okna";
            }
        }

        private async void OnEscapePressed(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await TryCloseAsync();
        }

        private async void OnExitRequested(object sender, RoutedEventArgs e) => await TryCloseAsync();

        /// <summary>
        /// Zamknięcie okna przechodzi tędy niezależnie od drogi: klawisz Esc, przycisk
        /// „Zakończ" albo krzyżyk na pasku tytułu. Niezapisane pomiary to cała sesja
        /// wzorcowania — zamknięcie bez pytania kasowałoby godziny pracy.
        /// </summary>
        private async void OnCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            if (!_dirty) return;

            var deferral = e.GetDeferral();
            try
            {
                e.Handled = !await ConfirmDiscardAsync();
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async Task TryCloseAsync()
        {
            if (_dirty && !await ConfirmDiscardAsync()) return;

            _engine.Stop();
            Application.Current.Exit();
        }

        /// <summary>Zwraca true, gdy wolno zamknąć aplikację.</summary>
        private async Task<bool> ConfirmDiscardAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "Niezapisane pomiary",
                Content = $"W mapie jest {_rows.Count} punktów, które nie zostały zapisane do pliku. " +
                          "Po zamknięciu przepadną.",
                PrimaryButtonText = "Zapisz i zamknij",
                SecondaryButtonText = "Zamknij bez zapisywania",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.Primary
            };

            var wynik = await dialog.ShowAsync();

            if (wynik == ContentDialogResult.Primary)
                return await SaveToFileAsync();   // brak zapisu (anulowany wybór pliku) wstrzymuje zamknięcie

            return wynik == ContentDialogResult.Secondary;
        }

        /// <summary>
        /// Sprawdza dostępność nowszej wersji i pokazuje własny pasek. Systemowe okno
        /// Instalatora aplikacji jest wyłączone w pliku .appinstaller, bo jego treści
        /// i opisu przycisków nie da się zmienić.
        /// </summary>
        private async Task CheckForUpdateAsync()
        {
            try
            {
                var dostepnosc = await Package.Current.CheckUpdateAvailabilityAsync();

                bool jest = dostepnosc.Availability == PackageUpdateAvailability.Available
                         || dostepnosc.Availability == PackageUpdateAvailability.Required;

                if (!jest) return;

                UpdateText.Text = dostepnosc.Availability == PackageUpdateAvailability.Required
                    ? "Dostępna jest wymagana aktualizacja aplikacji. Zapisz mapę przed aktualizacją."
                    : "Dostępna jest nowsza wersja aplikacji. Zapisz mapę przed aktualizacją.";

                UpdateBar.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Brak sieci albo instalacja spoza .appinstaller — nie jest to blad pracy.
                App.Log("CheckForUpdate", ex);
            }
        }

        private async void OnUpdateNow(object sender, RoutedEventArgs e)
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri(AppInstallerUri));
            }
            catch (Exception ex)
            {
                App.Log("OnUpdateNow", ex);
                UpdateStatus("nie udało się uruchomić aktualizacji: " + ex.Message);
            }
        }

        private void OnUpdateLater(object sender, RoutedEventArgs e) =>
            UpdateBar.Visibility = Visibility.Collapsed;

        private static string CurrentVersion()
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
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

            // Wersja aplikacji jest widoczna stale: aktualizacja podmienia kod syntezy bodzca,
            // a operator musi wiedziec, ktora wersja prowadzil sesje.
            parts.Add("wersja " + CurrentVersion());

            parts.Add(_rows.Count == 0
                ? "mapa jest pusta"
                : $"punktów w mapie: {_rows.Count}" + (_dirty ? " (niezapisane)" : ""));

            var missing = new List<string>();
            if (VolumeConfirmedBox.IsChecked != true) missing.Add("potwierdzenia głośności endpointu");
            if (_calibration.Verify == null || !_calibration.Verify.Passed) missing.Add("pozytywnej kontroli verify");

            // Braki w opisie stanowiska liczy rdzeń, tą samą metodą, ktorej uzyje bramka
            // dopuszczenia — operator widzi je od razu, a nie dopiero przy probie badania.
            BuildMetadata();
            var provenance = _calibration.CheckProvenance();
            missing.AddRange(provenance.Missing);

            if (missing.Count > 0)
                parts.Add("do dopuszczenia brakuje: " + string.Join(", ", missing));

            if (provenance.Incomplete.Count > 0)
                parts.Add("do opisu stanowiska brakuje: " + string.Join(", ", provenance.Incomplete));

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
