using System.Net.Mail;
using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    /// <summary>
    /// Matches the hint shown under the password field. The two must move
    /// together: a form that asks for eight characters and accepts four is
    /// telling the user something untrue about their own account.
    /// </summary>
    private const int MinimumPasswordLength = 8;

    private readonly ApiClient _api;

    public RegisterPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;

        AccessNoticeLabel.Text = AppInfo.AccessNotice;
        ServerLabel.Text = $"Server: {ApiConfig.BaseUrl}";
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        var name = NameField.Text?.Trim() ?? "";
        var email = EmailField.Text?.Trim() ?? "";
        var password = PasswordField.Text ?? "";
        var confirm = ConfirmField.Text ?? "";

        // Checked in the order the fields are read, so the message always
        // points at the first thing that needs fixing rather than the last.
        var problem = Validate(name, email, password, confirm);
        if (problem is not null)
        {
            ShowMessage(problem, isError: true);
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            await _api.RegisterAsync(email, password, name);

            // Sign in straight away. Someone who has just typed their password
            // twice should not be sent to a form to type it a third time.
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

    /// <summary>
    /// Returns the first problem with the form, or null if it is ready to send.
    ///
    /// This is convenience, not security: it saves a round trip and gives a
    /// clearer message than the API would. Anything that actually matters has
    /// to be enforced on the server, because a client can be bypassed.
    /// </summary>
    private static string? Validate(string name, string email, string password, string confirm)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirm))
            return "Fill in every field.";

        if (name.Length < 2)
            return "Enter your full name.";

        if (!LooksLikeEmail(email))
            return "That does not look like an email address.";

        if (password.Length < MinimumPasswordLength)
            return $"Use at least {MinimumPasswordLength} characters for your password.";

        if (password != confirm)
            // There is no password-reset endpoint, so a typo here would lock
            // the account permanently. Worth catching before it is sent.
            return "The two passwords do not match.";

        return null;
    }

    private static bool LooksLikeEmail(string value)
    {
        // MailAddress accepts things a clinic address never is -- "a@b" parses
        // cleanly -- so the dot in the domain is checked separately. The aim is
        // to catch a typo, not to prove the mailbox exists; only sending mail
        // to it could do that.
        if (!MailAddress.TryCreate(value, out var address)) return false;

        var host = address.Host;
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.');
    }

    private async void OnBackToSignInTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        CreateButton.IsEnabled = !busy;
        CreateButton.Text = busy ? "Creating account..." : "Create account";
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
