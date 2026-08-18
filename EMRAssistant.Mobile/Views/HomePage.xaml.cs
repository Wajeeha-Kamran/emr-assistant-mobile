using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

/// <summary>
/// One row of the attention card, with its presentation already resolved.
///
/// Colours and geometry are worked out in C# rather than by value converters in
/// XAML. Five reasons times four visual properties would be four converters and
/// twenty branches spread across a template; here the mapping is one table that
/// can be read in one go.
/// </summary>
public record AttentionRow(
    AttentionItem Item,
    string Title,
    string TimeText,
    string ActionText,
    Geometry? Icon,
    Color TileColor,
    Color InkColor,
    Brush InkBrush,
    bool ShowDivider);

public partial class HomePage : ContentPage
{
    private readonly ApiClient _api;
    private readonly ObservableCollection<AttentionRow> _rows = new();
    private bool _busy;

    public HomePage(ApiClient api)
    {
        InitializeComponent();
        _api = api;

        RowCommand = new Command<AttentionRow>(async row => await HandleRowAsync(row));
        BindableLayout.SetItemsSource(RowsHost, _rows);
    }

    /// <summary>Invoked when a row is tapped. Bound from the row template.</summary>
    public ICommand RowCommand { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    // -- loading ------------------------------------------------------------

    private async Task LoadAsync(bool showSpinner = true)
    {
        if (showSpinner) ShowState(loading: true);

        await LoadDoctorAsync();
        await LoadAttentionAsync();
    }

    private async Task LoadDoctorAsync()
    {
        DateLabel.Text = DateTime.Now.ToString("dddd, d MMMM");

        var hour = DateTime.Now.Hour;
        var partOfDay = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";

        // The salutation and the name are separate labels: the header sets them
        // at different sizes and weights, and a single string cannot do that.
        SalutationLabel.Text = $"{partOfDay},";

        try
        {
            var doctor = await _api.GetCurrentDoctorAsync();

            // The email is the fallback, and "Dr. name@clinic.com" reads as a
            // bug rather than a courtesy, so the title is only added to a real
            // name.
            GreetingLabel.Text = string.IsNullOrWhiteSpace(doctor.FullName)
                ? doctor.Email
                : WithTitle(doctor.FullName);
        }
        catch
        {
            // The greeting is decoration. Failing to personalise it must not
            // stop the part of the screen that matters from loading. The
            // salutation stands on its own without a name.
            SalutationLabel.Text = partOfDay;
            GreetingLabel.Text = "";
        }
    }

    /// <summary>
    /// Prefix "Dr." unless the doctor already typed it.
    ///
    /// Registration stores whatever name was entered, and some people include
    /// the title. Adding it unconditionally would produce "Dr. Dr Wajeeha".
    /// </summary>
    private static string WithTitle(string fullName)
    {
        var name = fullName.Trim();

        if (name.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Dr", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Prof", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"Dr. {name}";
    }

    private async Task LoadAttentionAsync()
    {
        try
        {
            var attention = await _api.GetAttentionAsync();

            _rows.Clear();
            for (var i = 0; i < attention.Items.Count; i++)
                _rows.Add(BuildRow(attention.Items[i], isLast: i == attention.Items.Count - 1));

            if (attention.Count == 0)
            {
                ShowState(empty: true);
            }
            else
            {
                CountLabel.Text = attention.Count == 1
                    ? "1 consultation needs action"
                    : $"{attention.Count} consultations need action";
                ShowState(list: true);
            }
        }
        catch (Exception ex)
        {
            // Never fall back to the empty state here. "All complete" and "I
            // could not find out" are different answers, and showing the
            // reassuring one when the truth is unknown is the single worst
            // thing this card can do.
            ErrorDetailLabel.Text = ex is ApiException
                ? ex.Message
                : "Your consultations may need attention.";
            ShowState(error: true);
        }
    }

    private void ShowState(bool loading = false, bool empty = false, bool error = false, bool list = false)
    {
        LoadingView.IsVisible = loading;
        EmptyView.IsVisible = empty;
        ErrorView.IsVisible = error;
        RowsHost.IsVisible = list;
        CountLabel.IsVisible = list;
    }

    // -- row presentation ---------------------------------------------------

    private AttentionRow BuildRow(AttentionItem item, bool isLast)
    {
        // Reason -> label, icon, tile colour, glyph colour. Each colour carries
        // its own fallback so a missing resource cannot turn an amber row
        // purple, which would be worse than no colour at all.
        var (title, icon, tile, tileFallback, ink, inkFallback) = item.Reason switch
        {
            AttentionReasons.TranscriptFailed =>
                ("Transcription failed", "IconWaveform", "TileWarning", "#FFF0E3", "InkWarning", "#E07B39"),
            AttentionReasons.TranscriptStalled =>
                ("Transcription didn't finish", "IconWaveformClock", "TileWarning", "#FFF0E3", "InkWarning", "#E07B39"),
            AttentionReasons.NoteNotGenerated =>
                ("Note not created", "IconDocument", "TileAccent", "#EDE7FB", "InkAccent", "#5B2E9D"),
            AttentionReasons.NotSigned =>
                ("Not signed", "IconPen", "TileAccent", "#EDE7FB", "InkAccent", "#5B2E9D"),
            AttentionReasons.SyncFailed =>
                ("Not sent to EMR", "IconCloudSlash", "TileDanger", "#FDE9E9", "InkDanger", "#E5484D"),

            // An unrecognised reason means the backend gained one this build
            // does not know about. Show it plainly rather than dropping it:
            // a stuck consultation the app hides is worse than an odd label.
            _ => ("Needs attention", "IconDocument", "TileAccent", "#EDE7FB", "InkAccent", "#5B2E9D"),
        };

        var action = item.Action switch
        {
            AttentionActions.ResumeTranscription => "Retry",
            AttentionActions.GenerateNote => "Create",
            AttentionActions.SignNote => "Open",
            AttentionActions.RetrySync => "Retry",
            _ => "Open",
        };

        var inkColour = BrandPalette.Color(ink, inkFallback);

        return new AttentionRow(
            Item: item,
            Title: title,
            TimeText: RelativeTime(item.CreatedAt),
            ActionText: action,
            Icon: IconGeometry(icon),
            TileColor: BrandPalette.Color(tile, tileFallback),
            InkColor: inkColour,
            InkBrush: new SolidColorBrush(inkColour),
            ShowDivider: !isLast);
    }

    private static Geometry? IconGeometry(string key)
        => Application.Current?.Resources is { } resources
           && resources.TryGetValue(key, out var value)
            ? value as Geometry
            : null;

    /// <summary>
    /// How long ago, in the words a person would use.
    ///
    /// The exact timestamp is not useful here — the doctor is identifying which
    /// consultation this was, and "18 min ago" does that better than "14:32".
    /// </summary>
    private static string RelativeTime(DateTimeOffset when)
    {
        var elapsed = DateTimeOffset.Now - when.ToLocalTime();

        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed < TimeSpan.FromHours(24)) return $"{(int)elapsed.TotalHours} hr ago";
        if (elapsed < TimeSpan.FromDays(2)) return "yesterday";
        if (elapsed < TimeSpan.FromDays(7)) return $"{(int)elapsed.TotalDays} days ago";

        return when.ToLocalTime().ToString("d MMM");
    }

    // -- actions ------------------------------------------------------------

    private async Task HandleRowAsync(AttentionRow? row)
    {
        if (row is null || _busy) return;

        var item = row.Item;

        // A draft waiting to be signed: open the note. The doctor is sent to the
        // review screen rather than straight to signing, because the reason this
        // consultation is on the list is that nobody has looked at the note yet
        // - and signing is what makes it a record. Review, codes and sign follow
        // from there.
        if (item.Action == AttentionActions.SignNote)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(ReviewPage)}?sessionId={item.SessionId}");
            return;
        }

        _busy = true;
        try
        {
            switch (item.Action)
            {
                case AttentionActions.ResumeTranscription:
                    await _api.RetryTranscriptionAsync(item.SessionId);
                    await DisplayAlert("Transcription restarted",
                        "It runs in the background and takes roughly as long as the recording. " +
                        "Pull down to refresh.", "OK");
                    break;

                case AttentionActions.GenerateNote:
                    await DisplayAlert("Creating the note",
                        "This runs the language model and can take up to half a minute.", "OK");
                    await _api.GenerateNoteAsync(item.SessionId);
                    break;

                case AttentionActions.RetrySync:
                    if (item.NoteId is not { } noteId) break;
                    await _api.RetrySyncAsync(noteId);
                    await DisplayAlert("Sending again",
                        "The note has been queued for the EMR. Pull down to refresh for the result.",
                        "OK");
                    break;
            }

            await LoadAttentionAsync();
        }
        catch (ApiException ex)
        {
            await DisplayAlert("That didn't work", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Unexpected error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnStartConsultationTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(RecordPage));

    private async void OnTryAgainClicked(object sender, EventArgs e) => await LoadAttentionAsync();

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadAsync(showSpinner: false);
        Refresher.IsRefreshing = false;
    }

    private async void OnSignOutTapped(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Sign out?",
            "You will need your email and password to sign back in.",
            "Sign out", "Cancel");

        if (!confirmed) return;

        _api.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }

    // -- hover feedback -----------------------------------------------------
    //
    // A Border has no pressed or hovered visual state, so the animation is done
    // by hand. Same values as HoverScaleBehavior uses on the real buttons, so
    // the two feel identical.

    // 480ms, not 280. At the shorter duration the button appeared to jump to
    // its new size rather than grow into it: the movement is only about half a
    // percent, so the eye reads a fast change as a step. A longer curve on a
    // small distance is what makes it feel like feedback instead of a glitch.
    private const uint HoverDuration = 480;

    private void OnStartPointerEntered(object sender, PointerEventArgs e)
        => StartButton.ScaleTo(1.018, HoverDuration, Easing.CubicOut);

    private void OnStartPointerExited(object sender, PointerEventArgs e)
        => StartButton.ScaleTo(1.0, HoverDuration, Easing.CubicOut);
}
