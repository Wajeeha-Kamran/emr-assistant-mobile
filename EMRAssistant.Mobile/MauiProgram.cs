using Microsoft.Extensions.Logging;
using EMRAssistant.Mobile.Services;
using EMRAssistant.Mobile.Views;

namespace EMRAssistant.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // ApiClient is a singleton so one HttpClient is shared for the life
            // of the app. Creating an HttpClient per request exhausts sockets --
            // a well-known .NET trap, avoided here from the first screen.
            builder.Services.AddSingleton<ApiClient>();

            // Pages are transient: a fresh instance each time one is shown, with
            // its dependencies supplied by the container.
            builder.Services.AddTransient<LoginPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}