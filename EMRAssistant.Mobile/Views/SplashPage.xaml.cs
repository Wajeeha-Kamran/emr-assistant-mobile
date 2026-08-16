using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

public partial class SplashPage : ContentPage
{
    private readonly ApiClient _api;

    public SplashPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;

        // The product name and tagline are part of logo_vertical.png, so only
        // the trust line is set from code.
        TrustLabel.Text = AppInfo.TrustLine;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        Loader.Start();

        // Long enough for the branding to register and for the artwork to be
        // seen, short enough not to annoy someone opening the app for the
        // twentieth time. The session check below often finishes well inside
        // this window, so the delay is what sets the pace, not the network.
        await Task.Delay(4000);

        var destination = "//LoginPage";

        if (await _api.TryRestoreSessionAsync())
        {
            // A stored token is not proof of a valid one -- it may have expired
            // since the last run. Verify it against the API before sending the
            // user to a screen that will immediately fail with a 401.
            try
            {
                await _api.GetCurrentDoctorAsync();
                destination = "//HomePage";
            }
            catch
            {
                _api.Logout();
            }
        }

        Loader.Stop();
        await Shell.Current.GoToAsync(destination);
    }
}
