using Microsoft.Maui.Controls.Shapes;

using Path = Microsoft.Maui.Controls.Shapes.Path;

using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

/// <summary>
/// Screen 9. Signing, and then the outcome of the push to the EMR.
///
/// The shape of this screen follows one fact: signing and syncing are different
/// events. POST /sign records the signature and returns; the EMR push is queued
/// behind it and runs in the background, where it can fail. So the screen shows
/// two facts separately, and the green tick on the signature stays green even
/// when the sync fails - the signature was never in doubt.
///
/// Leaving at any point is safe. The dashboard's attention list carries a
/// sync-failed row that brings the doctor back here.
/// </summary>
[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(NoteId), "noteId")]
public partial class SignPage : ContentPage
{
    private const int PollSeconds = 3;

    private readonly ApiClient _api;

    private SoapNote? _note;
    private Signature? _signature;
    private bool _confirmed;
    private bool _busy;
    private string? _syncStatus;
    private IDispatcherTimer? _poll;

    public SignPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    public string SessionId { get; set; } = "";
    public string NoteId { get; set; } = "";

    private int Session => int.TryParse(SessionId, out var id) ? id : 0;
    private int Note => int.TryParse(NoteId, out var id) ? id : 0;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPolling();
    }

    // -- loading ------------------------------------------------------------

    private async Task LoadAsync()
    {
        try
        {
            _note = await _api.GetSoapNoteAsync(Session);
        }
        catch (Exception ex)
        {
            LoadingView.IsVisible = false;
            HideFooter();
            MessageLabel.Text = ex is ApiException ? ex.Message : "The note could not be opened.";
            MessageLabel.IsVisible = true;
            return;
        }

        // Re-entering an already signed note must not offer the form again.
        if (_note.Status == SoapNoteStatuses.Signed)
        {
            LoadingView.IsVisible = false;
            ShowSigned(signedAtKnown: false);
            await RefreshSyncAsync();
            StartPolling();
            return;
        }

        HeaderMeta.Text = Subtitle(_note);
        await BuildSummaryAsync();
        BuildConsequences();

        LoadingView.IsVisible = false;
        ReviewView.IsVisible = true;
        PaintPrimary();
    }

    private static string Subtitle(SoapNote note)
    {
        // There is no GET /sessions/{id}, so the consultation's own times are
        // not available here. The note's creation time is, and it is the
        // honest thing to show: when this draft was produced.
        if (note.CreatedAt is not { } created) return "Draft ready to sign";

        var local = created.ToLocalTime();
        var day = local.Date == DateTimeOffset.Now.Date ? "today" : local.ToString("d MMM");
        return $"Drafted {day} at {local:HH:mm}";
    }

    // -- the summary --------------------------------------------------------

    private async Task BuildSummaryAsync()
    {
        SummaryHost.Clear();
        if (_note is null) return;

        foreach (var type in SoapSectionTypes.InOrder)
        {
            var section = _note.Sections.FirstOrDefault(s => s.SectionType == type);
            var words = WordCount(section?.Content);

            SummaryHost.Add(SummaryRow(
                letter: SoapSectionTypes.Letter(type),
                tint: SectionTint(type),
                ink: SectionInk(type),
                title: SoapSectionTypes.Title(type),
                detail: words == 0 ? "Empty" : $"{words} words",
                // An empty section does not block signing - a consultation can
                // genuinely produce nothing for one - but it should be visible
                // before the record is fixed.
                complete: words > 0));
        }

        SummaryHost.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#EFECF5"),
            Margin = new Thickness(0, 8),
        });

        var accepted = -1;
        try
        {
            var codes = await _api.GetCodeSuggestionsAsync(Note);
            accepted = codes.Count(c => c.Accepted);
        }
        catch
        {
            // The codes are optional. Their absence must not stop the doctor
            // signing a finished note.
        }

        SummaryHost.Add(SummaryRow(
            letter: "#",
            tint: Color.FromArgb("#EDE7FB"),
            ink: Color.FromArgb("#5B2E9D"),
            title: "Billing codes",
            detail: accepted < 0 ? "Unavailable"
                  : accepted == 0 ? "None accepted"
                  : $"{accepted} accepted",
            // No tick against "none accepted". Accepting a code is optional,
            // so this is neutral rather than complete.
            complete: accepted > 0));
    }

    private View SummaryRow(string letter, Color tint, Color ink, string title, string detail, bool complete)
    {
        var badge = new Border
        {
            WidthRequest = 34,
            HeightRequest = 34,
            Padding = 0,
            BackgroundColor = tint,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = letter,
                FontFamily = "PlayfairSemiBold",
                FontSize = 17,
                TextColor = ink,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            },
        };

        var text = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontFamily = "InterSemiBold",
                    FontSize = 13.5,
                    TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
                },
                new Label
                {
                    Text = detail,
                    FontFamily = "InterRegular",
                    FontSize = 11.5,
                    TextColor = complete
                        ? BrandPalette.Color("TextMuted", BrandPalette.TextMuted)
                        : BrandPalette.Color("InkWarning", "#E07B39"),
                },
            },
        };

        var mark = new Grid { WidthRequest = 26, HeightRequest = 26, VerticalOptions = LayoutOptions.Center };
        mark.Add(new Border
        {
            BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(complete
                ? BrandPalette.Color("SecureGreen", "#1F8A5B")
                : BrandPalette.Color("InkWarning", "#E07B39")),
            StrokeThickness = 1.6,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            InputTransparent = true,
        });
        if (complete)
        {
            mark.Add(new Path
            {
                Data = Icon("IconCheck"),
                Stroke = BrandPalette.Brush("SecureGreen", "#1F8A5B"),
                StrokeThickness = 2.2,
                StrokeLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brush.Transparent,
                Aspect = Stretch.Uniform,
                WidthRequest = 13,
                HeightRequest = 13,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
            });
        }

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 7),
        };
        row.Add(badge, 0);
        row.Add(text, 1);
        row.Add(mark, 2);
        return row;
    }

    private void BuildConsequences()
    {
        ConsequencesHost.Clear();

        // The third one is the consequence nobody predicts, and it is the one
        // that makes the retention design visible at the moment the doctor is
        // actually reading.
        var rows = new (string Icon, string Text)[]
        {
            ("IconLockClosed", "The note becomes a record and can no longer be edited"),
            ("IconUpload", "It is sent to the patient record system"),
            ("IconTrash", "The audio recording is deleted"),
        };

        foreach (var (icon, text) in rows)
        {
            var tile = new Grid { WidthRequest = 36, HeightRequest = 36, VerticalOptions = LayoutOptions.Center };
            tile.Add(new Border
            {
                BackgroundColor = BrandPalette.Color("TileAccent", "#EDE7FB"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                InputTransparent = true,
            });
            tile.Add(new Path
            {
                Data = Icon(icon),
                Stroke = BrandPalette.Brush("BrandPurple", BrandPalette.BrandPurpleLight),
                StrokeThickness = 1.6,
                StrokeLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brush.Transparent,
                Aspect = Stretch.Uniform,
                WidthRequest = 18,
                HeightRequest = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
            });

            var line = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                },
                ColumnSpacing = 12,
            };
            line.Add(tile, 0);
            line.Add(new Label
            {
                Text = text,
                FontFamily = "InterRegular",
                FontSize = 12.5,
                LineHeight = 1.35,
                TextColor = BrandPalette.Color("TextSecondary", "#6B6480"),
                VerticalOptions = LayoutOptions.Center,
            }, 1);

            ConsequencesHost.Add(line);
        }
    }

    // -- signing ------------------------------------------------------------

    private void OnConfirmToggled(object sender, EventArgs e)
    {
        if (_busy || _signature is not null) return;

        _confirmed = !_confirmed;
        if (_confirmed) HintLabel.IsVisible = false;
        CheckMark.IsVisible = _confirmed;
        CheckBox.BackgroundColor = _confirmed
            ? BrandPalette.Color("BrandPurple", BrandPalette.BrandPurpleLight)
            : Colors.Transparent;

        PaintPrimary();
    }

    /// <summary>
    /// The one place in this app where a disabled control is right: the disabled
    /// state is itself the message that something is required first.
    /// </summary>
    private void PaintPrimary()
    {
        var enabled = _confirmed && !_busy;

        PrimaryButton.Opacity = enabled ? 1 : 0.45;
        PrimaryFill.Background = enabled
            ? GradientBrush()
            : new SolidColorBrush(Color.FromArgb("#D8D4E4"));
        PrimaryLabel.TextColor = enabled ? Colors.White : BrandPalette.Color("TextMuted", BrandPalette.TextMuted);
    }

    private async void OnPrimaryTapped(object sender, EventArgs e)
    {
        // After signing this button changes job with the sync outcome, so it
        // has to branch on the outcome rather than on "is it signed".
        //
        // This was the bug behind "Back to dashboard does nothing": once the
        // sync succeeded the button was relabelled but still routed into
        // RetrySyncAsync, which returns immediately unless the sync FAILED. The
        // label said one thing and the handler did another.
        if (_signature is not null || _note?.Status == SoapNoteStatuses.Signed)
        {
            if (_syncStatus == SyncStatuses.Failed)
            {
                await RetrySyncAsync();
            }
            else
            {
                await Shell.Current.GoToAsync("//HomePage");
            }
            return;
        }

        if (_busy) return;

        if (!_confirmed)
        {
            // Previously this returned silently, which is indistinguishable
            // from a broken button - and that is exactly how it was read.
            HintLabel.IsVisible = true;
            FooterNote.IsVisible = false;
            await ConfirmRow.ScaleTo(1.04, 90, Easing.CubicOut);
            await ConfirmRow.ScaleTo(1.0, 90, Easing.CubicIn);
            return;
        }

        _busy = true;
        PrimaryLabel.Text = "Signing…";
        PaintPrimary();

        try
        {
            _signature = await _api.SignNoteAsync(Note);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Already signed. That is not an error to report - it is the state
            // this screen exists to show.
            _signature = null;
            _busy = false;
            ShowSigned(signedAtKnown: false);
            await RefreshSyncAsync();
            StartPolling();
            return;
        }
        catch (Exception ex)
        {
            _busy = false;
            PrimaryLabel.Text = "Sign and send to the EMR";
            PaintPrimary();
            await DisplayAlert("Not signed",
                ex is ApiException ? ex.Message : "The note could not be signed.", "OK");
            return;
        }

        _busy = false;
        ShowSigned(signedAtKnown: true);
        await RefreshSyncAsync();
        StartPolling();
    }

    private void ShowSigned(bool signedAtKnown)
    {
        StopPolling();

        HeaderTitle.Text = "Note signed";
        HeaderMeta.Text = "This consultation is now a record";

        ReviewView.IsVisible = false;
        LoadingView.IsVisible = false;
        SignedView.IsVisible = true;

        SignedMeta.Text = signedAtKnown && _signature is not null
            ? $"Signed at {_signature.SignedAt.ToLocalTime():HH:mm}"
            : "Signed";

        ConfirmRow.IsVisible = false;
        FooterNote.IsVisible = false;
        HintLabel.IsVisible = false;
        SecondaryLabel.IsVisible = true;
        PrimaryButton.IsVisible = false;
    }

    // -- the sync outcome ---------------------------------------------------

    private void StartPolling()
    {
        StopPolling();
        if (_syncStatus is SyncStatuses.Success or SyncStatuses.Failed) return;

        _poll = Dispatcher.CreateTimer();
        _poll.Interval = TimeSpan.FromSeconds(PollSeconds);
        _poll.Tick += async (_, _) => await RefreshSyncAsync();
        _poll.Start();
    }

    private void StopPolling()
    {
        _poll?.Stop();
        _poll = null;
    }

    private async Task RefreshSyncAsync()
    {
        try
        {
            _syncStatus = await _api.GetSyncStatusAsync(Note);
        }
        catch
        {
            // A dropped connection is not a failed sync. Keep polling and say
            // nothing new - claiming failure here would be a guess.
            return;
        }

        PaintSync();

        if (_syncStatus is SyncStatuses.Success or SyncStatuses.Failed) StopPolling();
    }

    private void PaintSync()
    {
        switch (_syncStatus)
        {
            case SyncStatuses.Success:
                SyncFill.BackgroundColor = BrandPalette.Color("TileSecure", "#E9F7F0");
                SyncTitle.Text = "Sent to the patient record system";
                SyncTitle.TextColor = BrandPalette.Color("SecureGreen", "#1F8A5B");
                SyncBody.Text = "The recording has been scheduled for deletion.";
                SyncBody.IsVisible = true;

                PrimaryButton.IsVisible = true;
                PrimaryLabel.Text = "Back to dashboard";
                PrimaryFill.Background = GradientBrush();
                PrimaryButton.Opacity = 1;
                PrimaryLabel.TextColor = Colors.White;
                SecondaryLabel.IsVisible = false;
                break;

            case SyncStatuses.Failed:
                SyncFill.BackgroundColor = BrandPalette.Color("TileWarning", "#FFF0E3");
                SyncTitle.Text = "Couldn't reach the patient record system";
                SyncTitle.TextColor = BrandPalette.Color("InkWarning", "#E07B39");
                SyncBody.Text = "The note is signed and safe. Only the delivery failed.";
                SyncBody.IsVisible = true;

                // Retry is offered ONLY here. The endpoint refuses a retry while
                // a sync is still pending, deliberately, so that a note cannot
                // be delivered to the EMR twice.
                PrimaryButton.IsVisible = true;
                PrimaryLabel.Text = "Try sending again";
                PrimaryFill.Background = GradientBrush();
                PrimaryButton.Opacity = 1;
                PrimaryLabel.TextColor = Colors.White;
                SecondaryLabel.IsVisible = true;
                break;

            default:
                SyncFill.BackgroundColor = BrandPalette.Color("TileAccent", "#EDE7FB");
                SyncTitle.Text = "Sending to the patient record system…";
                SyncTitle.TextColor = BrandPalette.Color("InkAccent", BrandPalette.BrandPurpleLight);
                SyncBody.IsVisible = false;

                PrimaryButton.IsVisible = false;
                SecondaryLabel.IsVisible = true;
                break;
        }
    }

    private async Task RetrySyncAsync()
    {
        if (_syncStatus != SyncStatuses.Failed) return;

        PrimaryLabel.Text = "Sending…";
        try
        {
            await _api.RetrySyncAsync(Note);
            _syncStatus = SyncStatuses.Pending;
            PaintSync();
            StartPolling();
        }
        catch (Exception ex)
        {
            PrimaryLabel.Text = "Try sending again";
            await DisplayAlert("Not sent",
                ex is ApiException ? ex.Message : "The note could not be sent.", "OK");
        }
    }

    // -- helpers ------------------------------------------------------------

    private void HideFooter()
    {
        ConfirmRow.IsVisible = false;
        PrimaryButton.IsVisible = false;
        FooterNote.IsVisible = false;
        HintLabel.IsVisible = false;
        SecondaryLabel.IsVisible = true;
    }

    private static int WordCount(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static Color SectionTint(string type) => type switch
    {
        SoapSectionTypes.Subjective => Color.FromArgb("#EDE7FB"),
        SoapSectionTypes.Objective => Color.FromArgb("#E4EEFB"),
        SoapSectionTypes.Assessment => Color.FromArgb("#E2F4F1"),
        _ => Color.FromArgb("#FFF0E3"),
    };

    private static Color SectionInk(string type) => type switch
    {
        SoapSectionTypes.Subjective => Color.FromArgb("#5B2E9D"),
        SoapSectionTypes.Objective => Color.FromArgb("#2C5C9E"),
        SoapSectionTypes.Assessment => Color.FromArgb("#0F6E62"),
        _ => Color.FromArgb("#B4611F"),
    };

    /// <summary>The shared purple button gradient, with a flat fallback.</summary>
    private static Brush GradientBrush()
        => Application.Current?.Resources is { } resources
           && resources.TryGetValue("ButtonGradient", out var value)
           && value is Brush brush
            ? brush
            : new SolidColorBrush(BrandPalette.Color("BrandPurple", BrandPalette.BrandPurpleLight));

    private static Geometry? Icon(string key)
        => Application.Current?.Resources is { } resources
           && resources.TryGetValue(key, out var value)
            ? value as Geometry
            : null;

    // -- navigation ---------------------------------------------------------

    private async void OnReviewAgainTapped(object sender, EventArgs e)
    {
        // Popping with ".." did nothing from this page. Navigating to the route
        // explicitly always works, and the note screen reloads its own state
        // from the server anyway.
        try
        {
            await Shell.Current.GoToAsync($"{nameof(ReviewPage)}?sessionId={Session}");
        }
        catch
        {
            await Shell.Current.GoToAsync("//HomePage");
        }
    }

    private async void OnDashboardTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//HomePage");
}
