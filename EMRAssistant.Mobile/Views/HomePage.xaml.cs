using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly ApiClient _api;

    public HomePage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var doctor = await _api.GetCurrentDoctorAsync();
            var name = string.IsNullOrWhiteSpace(doctor.FullName) ? doctor.Email : doctor.FullName;
            GreetingLabel.Text = $"Hello, {name}";
            SubLabel.Text = $"Signed in to {AppInfo.ProductName}";
        }
        catch
        {
            // The token was accepted at sign-in but is not working now, so it
            // has expired. Send the user back rather than showing a broken page.
            _api.Logout();
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        _api.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
