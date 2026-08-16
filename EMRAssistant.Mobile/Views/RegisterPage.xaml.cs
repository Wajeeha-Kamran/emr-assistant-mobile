using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    private readonly ApiClient _api;

    public RegisterPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        var name = NameField.Text?.Trim() ?? "";
        var email = EmailField.Text?.Trim() ?? "";
        var password = PasswordField.Text ?? "";

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Fill in every field.", isError: true);
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            await _api.RegisterAsync(email, password, name);

            // Sign in straight away. Making someone who just typed their
            // password type it again is friction with no purpose.
            await _api.LoginAsync(email, password);
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

    private async void OnBackToSignInClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        CreateButton.IsEnabled = !busy;
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
