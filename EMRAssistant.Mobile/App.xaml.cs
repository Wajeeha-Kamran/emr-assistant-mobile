namespace EMRAssistant.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }

        /// <summary>
        /// Open at phone proportions when running on Windows.
        ///
        /// The Windows target is being used for development because the Android
        /// SDK is not installed yet. Left to itself it opens as a wide desktop
        /// window, which makes a mobile layout impossible to judge and produces
        /// screenshots that look nothing like the product. Constraining the
        /// window to roughly a handset aspect ratio means what is on screen is
        /// what a phone would show.
        ///
        /// This affects Windows only. On Android and iOS the platform decides.
        /// </summary>
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = base.CreateWindow(activationState);

#if WINDOWS
            const int phoneWidth = 420;
            const int phoneHeight = 880;

            window.Width = phoneWidth;
            window.Height = phoneHeight;
            window.MinimumWidth = phoneWidth;
            window.MinimumHeight = 600;
            window.MaximumWidth = phoneWidth;
#endif

            return window;
        }
    }
}