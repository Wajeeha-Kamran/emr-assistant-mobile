using Microsoft.Maui.Controls.Shapes;

using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

/// <summary>
/// Screen 6. Watches the pipeline and moves on by itself.
///
/// The chaining is not a shortcut: UC-02's special requirement in the SRS says
/// "Draft note should be generated after stop without manual triggering", so
/// the doctor presses nothing between finishing the recording and reviewing the
/// note.
/// </summary>
[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(RecordedLength), "length")]
public partial class ProcessingPage : ContentPage
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private enum Failure { None, Transcription, Drafting }

    private readonly ApiClient _api;

    private CancellationTokenSource? _polling;
    private DateTime _startedAt = DateTime.Now;
    private Failure _failure = Failure.None;

    public ProcessingPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    public string SessionId { get; set; } = "";
    public string RecordedLength { get; set; } = "";

    private int Session => int.TryParse(SessionId, out var id) ? id : 0;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _startedAt = DateTime.Now;
        StartElapsedClock();
        _ = RunAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Leaving stops the polling, not the work. The pipeline runs on the
        // server, which is exactly what the reassurance strip promises.
        _polling?.Cancel();
    }

    // -- the two stages -----------------------------------------------------

    private async Task RunAsync()
    {
        _polling?.Cancel();
        _polling = new CancellationTokenSource();
        var token = _polling.Token;

        _failure = Failure.None;
        ShowStages();
        SetStage(one: "Starting…", oneDone: false, two: "Waiting", twoActive: false);

        // ---- stage one: transcription and diarization ----
        try
        {
            while (!token.IsCancellationRequested)
            {
                var transcript = await _api.GetTranscriptAsync(Session);

                if (transcript.Status == TranscriptStatuses.Completed)
                {
                    SetStage(one: "Complete", oneDone: true, two: "Working…", twoActive: true);
                    break;
                }

                if (transcript.Status == TranscriptStatuses.Failed)
                {
                    ShowFailure(Failure.Transcription);
                    return;
                }

                SetStage(one: "Working…", oneDone: false, two: "Waiting", twoActive: false);
                await Task.Delay(PollInterval, token);
            }
        }
        catch (TaskCanceledException) { return; }
        catch (Exception)
        {
            // A dropped connection is not a failed transcription. The work is
            // still running on the server, so say so rather than offering to
            // start it again.
            SetStage(one: "Waiting for the server…", oneDone: false, two: "Waiting", twoActive: false);
            try { await Task.Delay(PollInterval, token); } catch { return; }
            if (!token.IsCancellationRequested) _ = RunAsync();
            return;
        }

        if (token.IsCancellationRequested) return;

        // ---- stage two: the draft ----
        try
        {
            await _api.GenerateSoapNoteAsync(Session);
        }
        catch (Exception)
        {
            ShowFailure(Failure.Drafting);
            return;
        }

        if (token.IsCancellationRequested) return;

        SetStage(one: "Complete", oneDone: true, two: "Complete", twoActive: false, twoDone: true);
        await Task.Delay(600);

        await Shell.Current.GoToAsync($"{nameof(ReviewPage)}?sessionId={Session}");
    }

    // -- presentation -------------------------------------------------------

    private void SetStage(string one, bool oneDone, string two, bool twoActive, bool twoDone = false)
    {
        var green = BrandPalette.Color("SecureGreen", "#1F8A5B");
        var greenTile = BrandPalette.Color("TileSecure", "#E9F7F0");
        var purple = BrandPalette.Color("BrandPurple", "#5B2E9D");
        var purpleTile = BrandPalette.Color("TileAccent", "#EDE7FB");
        var muted = BrandPalette.Color("TextMuted", BrandPalette.TextMuted);
        var mutedTile = BrandPalette.Color("SurfaceMuted", "#F4F2F9");

        StageOneStatus.Text = one;
        StageOneStatus.TextColor = oneDone ? green : purple;
        StageOneTile.BackgroundColor = oneDone ? greenTile : purpleTile;
        StageOneIcon.Stroke = new SolidColorBrush(oneDone ? green : purple);
        StageOneMark.Text = oneDone ? "✓" : "•••";
        StageOneMark.TextColor = oneDone ? green : purple;

        Connector.Color = oneDone ? green : BrandPalette.Color("FieldBorder", "#DCD7E8");

        StageTwoStatus.Text = two;
        StageTwoStatus.TextColor = twoDone ? green : twoActive ? purple : muted;
        StageTwoTile.BackgroundColor = twoDone ? greenTile : twoActive ? purpleTile : mutedTile;
        StageTwoIcon.Stroke = new SolidColorBrush(twoDone ? green : twoActive ? purple : muted);
        StageTwoMark.Text = twoDone ? "✓" : twoActive ? "•••" : "◷";
        StageTwoMark.TextColor = twoDone ? green : twoActive ? purple : muted;

        StageTwoTile.Opacity = twoActive || twoDone ? 1.0 : 0.55;
    }

    private void ShowStages()
    {
        StagesView.IsVisible = true;
        FailureView.IsVisible = false;
        RetryButton.IsVisible = false;
        SecondaryButton.IsVisible = false;
        ReassuranceStrip.IsVisible = true;
    }

    private void ShowFailure(Failure failure)
    {
        _failure = failure;

        StagesView.IsVisible = false;
        FailureView.IsVisible = true;
        RetryButton.IsVisible = true;
        SecondaryButton.IsVisible = true;

        // The reassurance strip promises the work carries on. Once something
        // has failed that is no longer true, so it comes off.
        ReassuranceStrip.IsVisible = false;

        if (failure == Failure.Transcription)
        {
            FailureTile.BackgroundColor = BrandPalette.Color("TileWarning", "#FFF0E3");
            FailureIcon.Data = Geometry("IconWaveformClock");
            FailureIcon.Stroke = BrandPalette.Brush("InkWarning", "#E07B39");
            FailureTitle.Text = "Transcription didn't finish";
            FailureBody.Text = "The recording is safe on the server. This can be run again.";
            SecondaryButton.Text = "Back to dashboard";
        }
        else
        {
            FailureTile.BackgroundColor = BrandPalette.Color("TileAccent", "#EDE7FB");
            FailureIcon.Data = Geometry("IconDocument");
            FailureIcon.Stroke = BrandPalette.Brush("BrandPurple", BrandPalette.BrandPurpleLight);
            FailureTitle.Text = "The note wasn't drafted";
            FailureBody.Text = "The transcript is ready. Drafting can be tried again.";

            // Not "back to dashboard". The transcript exists and is worth
            // reading even though the next step stumbled; sending the doctor
            // away would strand a consultation the attention list then has to
            // rescue.
            SecondaryButton.Text = "View the transcript";
        }
    }

    private static Geometry? Geometry(string key)
        => Application.Current?.Resources is { } resources
           && resources.TryGetValue(key, out var value)
            ? value as Geometry
            : null;

    /// <summary>
    /// Elapsed, not remaining. The app cannot know how long is left, and an
    /// estimate that overruns is worse than none at all.
    /// </summary>
    private void StartElapsedClock()
    {
        var recorded = string.IsNullOrWhiteSpace(RecordedLength) ? "" : $"Recorded {RecordedLength} · ";
        SubtitleLabel.Text = recorded + "started just now";

        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(10);
        timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _startedAt;
            var since = elapsed < TimeSpan.FromMinutes(1)
                ? "started just now"
                : $"started {(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes == 1 ? "" : "s")} ago";
            SubtitleLabel.Text = recorded + since;
        };
        timer.Start();
    }

    // -- actions ------------------------------------------------------------

    private async void OnRetryTapped(object sender, EventArgs e)
    {
        if (_failure == Failure.Transcription)
        {
            try
            {
                await _api.RetryTranscriptionAsync(Session);
            }
            catch (ApiException ex)
            {
                await DisplayAlert("Could not restart", ex.Message, "OK");
                return;
            }
        }

        _startedAt = DateTime.Now;
        _ = RunAsync();
    }

    private async void OnSecondaryClicked(object sender, EventArgs e)
    {
        if (_failure == Failure.Drafting)
            await Shell.Current.GoToAsync($"{nameof(ReviewPage)}?sessionId={Session}");
        else
            await Shell.Current.GoToAsync("//HomePage");
    }

    private async void OnBackTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//HomePage");
}
