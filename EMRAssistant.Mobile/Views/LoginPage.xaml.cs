using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly ApiClient _api;

    public LoginPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;

        NewHereLabel.Text = $"New to {AppInfo.ProductName}?";
        AccessNoticeLabel.Text = AppInfo.AccessNotice;
        ServerLabel.Text = $"Server: {ApiConfig.BaseUrl}";
    }

    private async void OnSignInClicked(object sender, EventArgs e)
    {
        var email = EmailField.Text?.Trim() ?? "";
        var password = PasswordField.Text ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Enter your email and password.", isError: true);
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            await _api.LoginAsync(email, password, rememberMe: RememberMe.IsChecked);
            await Shell.Current.GoToAsync("//HomePage");
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

    private async void OnCreateAccountTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("RegisterPage");

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        SignInButton.IsEnabled = !busy;
        SignInButton.Text = busy ? "Signing in..." : "Sign in securely";
    }

    private void ShowMessage(string text, bool isError)
    {
        MessageLabel.Text = text;
        MessageLabel.TextColor = (Color)Application.Current!.Resources[isError ? "Danger" : "Success"];
        MessageCard.BackgroundColor = (Color)Application.Current!.Resources[isError ? "DangerBg" : "SuccessBg"];
        MessageCard.IsVisible = true;
    }

    private void HideMessage() => MessageCard.IsVisible = false;
}
