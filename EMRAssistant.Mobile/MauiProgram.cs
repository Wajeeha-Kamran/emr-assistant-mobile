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
                    // Poppins for display type: geometric and characterful, used
                    // for headings where personality is wanted.
                    fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");
                    fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");

                    // Inter for everything a clinician actually reads. It was
                    // designed for screens and stays legible at small sizes,
                    // which matters most on the transcript and SOAP screens.
                    //
                    // The 18pt files are Inter's optical size intended for user
                    // interface text; the 24pt and 28pt cuts are for large
                    // display use and are not registered.
                    fonts.AddFont("Inter_18pt-Regular.ttf", "InterRegular");
                    fonts.AddFont("Inter_18pt-Medium.ttf", "InterMedium");
                    fonts.AddFont("Inter_18pt-SemiBold.ttf", "InterSemiBold");

                    // Retained: the template registers these and removing them
                    // would break anything still referring to them.
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // ApiClient is a singleton so one HttpClient is shared for the life
            // of the app. Creating an HttpClient per request exhausts sockets --
            // a well-known .NET trap, avoided here from the first screen.
            builder.Services.AddSingleton<ApiClient>();

            // Pages are transient: a fresh instance each time one is shown, with
            // its dependencies supplied by the container.
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<HomePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

            // Remove the platform's own Entry border and background.
            //
            // Every Entry sits inside our own bordered container (IconEntry), so
            // the native border draws a second outline within the first --
            // visible as a box around the placeholder text. There is no
            // cross-platform property for this; it has to be done per platform
            // on the underlying control.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
                "BorderlessEntry", (handler, view) =>
                {
#if WINDOWS
                    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    handler.PlatformView.Background = null;
                    handler.PlatformView.FocusVisualMargin = new Microsoft.UI.Xaml.Thickness(0);
#elif ANDROID
                    handler.PlatformView.Background = null;
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#endif
                });

            return builder.Build();
        }
    }
}