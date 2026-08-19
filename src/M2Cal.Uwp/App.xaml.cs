using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
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
            UnhandledException += (s, e) => Log("UnhandledException", e.Exception);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                if (!(Window.Current.Content is Frame rootFrame))
                {
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;
                }

                if (!e.PrelaunchActivated && rootFrame.Content == null)
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);

                Window.Current.Activate();
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
