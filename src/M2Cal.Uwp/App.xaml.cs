using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace M2Cal.Uwp
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            UnhandledException += (s, e) =>
            {
                Log("UnhandledException", e.Exception);

                // Świadomy kompromis: przerwanie procesu kasuje całą sesję pomiarową, a operator
                // musiałby powtórzyć wzorcowanie od zera. Lepiej zostawić okno przy życiu i zapisać
                // przyczynę do dziennika niż stracić zmierzone punkty.
                e.Handled = true;
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                // Okno zmaksymalizowane, a NIE pelnoekranowe. Pelny ekran ukrywa pasek tytulu
                // razem z przyciskami minimalizacji i zamkniecia, co przy pracy zdalnej odcina
                // jedyna droge wyjscia. Pelny ekran zostaje dostepny z przycisku w oknie.
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Maximized;

                if (!(Window.Current.Content is Frame rootFrame))
                {
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;
                }

                if (!e.PrelaunchActivated && rootFrame.Content == null)
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);

                Window.Current.Activate();

                // PreferredLaunchWindowingMode dziala od kolejnego uruchomienia. Pierwsze po
                // aktualizacji z wersji pelnoekranowej trzeba przelaczyc jawnie, inaczej okno
                // zostaje bez paska tytulu.
                var view = ApplicationView.GetForCurrentView();
                if (view.IsFullScreenMode) view.ExitFullScreenMode();
                view.TryResizeView(new Windows.Foundation.Size(1600, 1000));
            }
            catch (System.Exception ex)
            {
                Log("OnLaunched", ex);
                throw;
            }
        }

        /// <summary>
        /// Zapisuje wyjątek startowy do LocalState. Bez tego awaria przy uruchomieniu widać
        /// wyłącznie jako kod wyjątku CLR w dzienniku zdarzeń, bez stosu wywołań.
        /// </summary>
        internal static void Log(string stage, System.Exception ex)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    Windows.Storage.ApplicationData.Current.LocalFolder.Path, "startup-error.txt");

                System.IO.File.AppendAllText(path, $"[{stage}] {ex}\n\n");
            }
            catch (System.Exception)
            {
                // logowanie awarii nie może wywołać kolejnej awarii
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new System.Exception("nie udało się otworzyć strony " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
