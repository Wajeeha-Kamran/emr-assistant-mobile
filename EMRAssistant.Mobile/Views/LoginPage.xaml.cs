using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

/// <summary>
/// The first screen, and the one that proves the whole integration works:
/// connectivity, authentication, and secure token storage. Everything after
/// this is the same pattern applied to different endpoints.
/// </summary>
public partial class LoginPage : ContentPage
{
    private readonly ApiClient _api;
    private bool _registerMode;

    public LoginPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;

        // Showing the address the app is actually calling turns "it doesn't
        // work" into "it is calling the wrong host", which is the single most
        // common problem when a mobile client meets a local API.
        ServerLabel.Text = $"Server: {ApiConfig.BaseUrl}";
    }

    private void OnToggleModeClicked(object sender, EventArgs e)
    {
        _registerMode = !_registerMode;

        FullNameEntry.IsVisible = _registerMode;
        PrimaryButton.Text = _registerMode ? "Create account" : "Sign in";
        ToggleModeButton.Text = _registerMode
            ? "I already have an account"
            : "Create an account instead";

        HideMessage();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";
        var fullName = FullNameEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Enter your email and password.", isError: true);
            return;
        }

        if (_registerMode && string.IsNullOrWhiteSpace(fullName))
        {
            ShowMessage("Enter your full name.", isError: true);
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            if (_registerMode)
            {
                await _api.RegisterAsync(email, password, fullName);
            }

            await _api.LoginAsync(email, password);

            // Obtaining a token and successfully USING one are different things.
            // Calling /auth/me proves the token is actually accepted, which is
            // what this screen is meant to establish.
            var doctor = await _api.GetCurrentDoctorAsync();

            ShowMessage(
                $"Signed in as {(string.IsNullOrEmpty(doctor.FullName) ? doctor.Email : doctor.FullName)}." +
                $"\nDoctor id {doctor.Id}. The token works.",
                isError: false);
        }
        catch (ApiException ex)
        {
            ShowMessage(ex.Message, isError: true);
        }
        catch (Exception ex)
        {
            ShowMessage($"Unexpected error: {ex.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        PrimaryButton.IsEnabled = !busy;
        ToggleModeButton.IsEnabled = !busy;
    }

    private void ShowMessage(string text, bool isError)
    {
        MessageLabel.Text = text;
        MessageLabel.TextColor = isError ? Color.FromArgb("#8C1D18") : Color.FromArgb("#0F5132");
        MessageFrame.BackgroundColor = isError ? Color.FromArgb("#F9DEDC") : Color.FromArgb("#D1E7DD");
        MessageFrame.IsVisible = true;
    }

    private void HideMessage() => MessageFrame.IsVisible = false;
}
