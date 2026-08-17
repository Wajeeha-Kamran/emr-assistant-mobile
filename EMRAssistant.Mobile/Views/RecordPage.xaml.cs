using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

public partial class RecordPage : ContentPage
{
    /// <summary>
    /// Matches audio_manager.py, which rejects anything longer with a 400 —
    /// and does so AFTER the upload completes. Stopping here is what stops a
    /// doctor recording 35 minutes, waiting through the upload, and losing it.
    /// </summary>
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    /// <summary>The point at which the limit stops being trivia and starts mattering.</summary>
    private static readonly TimeSpan WarnAt = TimeSpan.FromMinutes(27);

    private enum Stage
    {
        Permission,
        Ready,
        Recording,   // live capture, clock running
        Selected,    // a finished file is held, waiting to be sent
        Uploading,
        Failed,
    }

    private readonly ApiClient _api;
    private readonly IConsultationRecorder _recorder;

    private Stage _stage;
    private IDispatcherTimer? _timer;
    private DateTime _startedAt;
    private TimeSpan _elapsed;
    private RecordedAudio? _recorded;
    private int? _sessionId;

    public RecordPage(ApiClient api, IConsultationRecorder recorder)
    {
        InitializeComponent();
        _api = api;
        _recorder = recorder;

        GoTo(_recorder.NeedsPermission ? Stage.Permission : Stage.Ready);
    }

    // -- state --------------------------------------------------------------

    private void GoTo(Stage stage)
    {
        _stage = stage;

        // The header heading is not fixed. Two of these states happen before
        // the consultation starts and two after it has ended, and
        // "Consultation in progress" is untrue in all four.
        HeaderTitle.Text = stage switch
        {
            Stage.Permission or Stage.Ready => "New consultation",
            Stage.Recording or Stage.Selected => "Consultation in progress",
            _ => "Finishing up",
        };

        PermissionView.IsVisible = stage == Stage.Permission;
        TimerView.IsVisible = stage is Stage.Ready or Stage.Recording or Stage.Selected;
        UploadingView.IsVisible = stage == Stage.Uploading;
        FailedView.IsVisible = stage == Stage.Failed;

        FinishControl.IsVisible = stage == Stage.Recording;
        DiscardButton.IsVisible = stage == Stage.Failed;

        // No control at all while uploading. There is nothing safe to press
        // with a file in flight.
        WideButton.IsVisible = stage is Stage.Permission or Stage.Ready
                                        or Stage.Selected or Stage.Failed;
        WideButtonLabel.Text = stage switch
        {
            Stage.Permission => "Allow microphone",
            Stage.Ready => _recorder.CapturesLive ? "Start recording" : "Choose a recording",
            Stage.Selected => "Upload recording",
            _ => "Retry upload",
        };
        WideButtonIcon.IsVisible = stage is Stage.Permission or Stage.Ready;

        SelectedFileLabel.IsVisible = stage == Stage.Selected;

        if (stage == Stage.Ready) ShowReadyTimer();
    }

    private void ShowReadyTimer()
    {
        StatusLabel.Text = "READY";
        StatusDot.Fill = BrandPalette.Brush("TextMuted", BrandPalette.TextMuted);
        StatusLabel.TextColor = BrandPalette.Color("TextMuted", BrandPalette.TextMuted);
        TimerLabel.Text = "00:00";
        LimitLabel.TextColor = BrandPalette.Color("TextMuted", BrandPalette.TextMuted);
        LimitWarningLabel.IsVisible = false;

        // Flat and dim. A live-looking waveform before Start would imply audio
        // is already being captured, which is the one impression a recording
        // app must never give.
        WaveformLabel.Opacity = 0.35;
        WaveformLabel.FontSize = 15;
    }

    // -- actions ------------------------------------------------------------

    private async void OnWideButtonTapped(object sender, EventArgs e)
    {
        switch (_stage)
        {
            case Stage.Permission:
                if (await _recorder.RequestPermissionAsync())
                    GoTo(Stage.Ready);
                else
                    await DisplayAlert("Microphone blocked",
                        "The consultation cannot be recorded without microphone access. " +
                        "You can grant it in system settings.", "OK");
                break;

            case Stage.Ready:
                await StartRecordingAsync();
                break;

            case Stage.Selected:
            case Stage.Failed:
                await UploadAsync();
                break;
        }
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            await _recorder.StartAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could not start recording", ex.Message, "OK");
            return;
        }

        try
        {
            // The session is created here, not when the screen opened and not
            // when the recording finishes, so the server observes UC-01 as it
            // happens rather than being told afterwards.
            _sessionId = await _api.CreateSessionAsync();
            await _api.StartRecordingAsync(_sessionId.Value);
        }
        catch (Exception ex)
        {
            _recorder.Discard();
            await DisplayAlert("Could not start the consultation", ex.Message, "OK");
            return;
        }

        // A supplied file is already complete. Showing RECORDING over a
        // ticking clock would claim audio is being captured when none is, and
        // the clock would be counting how long the doctor looked at the screen
        // rather than how long the consultation was.
        if (!_recorder.CapturesLive)
        {
            await ShowSelectedFileAsync();
            return;
        }

        _startedAt = DateTime.Now;
        _elapsed = TimeSpan.Zero;

        StatusLabel.Text = "RECORDING";
        StatusDot.Fill = BrandPalette.Brush("InkDanger", BrandPalette.Danger);
        StatusLabel.TextColor = BrandPalette.Color("InkDanger", BrandPalette.Danger);
        WaveformLabel.Opacity = 1.0;
        WaveformLabel.FontSize = 22;

        GoTo(Stage.Recording);
        StartTimer();
    }

    /// <summary>
    /// The file is chosen and its real length is known, so show that rather
    /// than a stopwatch. The limit is checked here, before the upload is spent
    /// on a file the server is going to reject.
    /// </summary>
    private async Task ShowSelectedFileAsync()
    {
        _recorded = await _recorder.StopAsync();

        var length = _recorded.Duration;

        if (length > MaxDuration)
        {
            await DisplayAlert("Recording too long",
                $"This recording is {(int)length.TotalMinutes} minutes. The limit is 30, and the " +
                "server rejects anything longer after the upload has already finished. " +
                "Choose a shorter recording.", "OK");
            _recorder.Discard();
            _recorded = null;
            GoTo(Stage.Ready);
            return;
        }

        StatusLabel.Text = "READY TO UPLOAD";
        StatusDot.Fill = BrandPalette.Brush("SecureGreen", "#1F8A5B");
        StatusLabel.TextColor = BrandPalette.Color("SecureGreen", "#1F8A5B");

        TimerLabel.Text = length > TimeSpan.Zero
            ? $"{(int)length.TotalMinutes:00}:{length.Seconds:00}"
            : "--:--";
        LimitLabel.TextColor = BrandPalette.Color("TextMuted", BrandPalette.TextMuted);
        LimitWarningLabel.IsVisible = false;

        WaveformLabel.Opacity = 0.55;
        WaveformLabel.FontSize = 18;

        SelectedFileLabel.Text =
            $"{Path.GetFileName(_recorded.FilePath)}  ·  {_recorded.Bytes / 1024d / 1024d:0.0} MB";

        GoTo(Stage.Selected);
    }

    private void StartTimer()
    {
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) =>
        {
            _elapsed = DateTime.Now - _startedAt;
            TimerLabel.Text = $"{(int)_elapsed.TotalMinutes:00}:{_elapsed.Seconds:00}";

            if (_elapsed >= WarnAt && !LimitWarningLabel.IsVisible)
            {
                LimitWarningLabel.IsVisible = true;
                LimitLabel.TextColor = BrandPalette.Color("InkWarning", "#E07B39");
                StatusDot.Fill = BrandPalette.Brush("InkWarning", "#E07B39");
                StatusLabel.TextColor = BrandPalette.Color("InkWarning", "#E07B39");
            }

            // Stops itself rather than letting the server reject the upload.
            if (_elapsed >= MaxDuration) _ = FinishAsync();
        };
        _timer.Start();
    }

    private async void OnFinishTapped(object sender, EventArgs e) => await FinishAsync();

    private async Task FinishAsync()
    {
        if (_stage != Stage.Recording) return;

        _timer?.Stop();

        try
        {
            _recorded = await _recorder.StopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Nothing was recorded", ex.Message, "OK");
            GoTo(Stage.Ready);
            return;
        }

        await UploadAsync();
    }

    private async Task UploadAsync()
    {
        if (_recorded is null || _sessionId is null) return;

        GoTo(Stage.Uploading);

        var length = _recorded.Duration > TimeSpan.Zero ? _recorded.Duration : _elapsed;
        UploadLengthLabel.Text =
            $"Consultation length {(int)length.TotalMinutes:00}:{length.Seconds:00}";
        UploadSizeLabel.Text = $"0.0 MB of {_recorded.Bytes / 1024d / 1024d:0.0} MB";
        UploadProgress.Progress = 0;

        // HttpClient reports no upload progress, so this is an indeterminate
        // wait dressed as a determinate one. It creeps toward 90% and only
        // completes when the request actually returns - it never claims to be
        // finished before it is.
        var creep = Dispatcher.CreateTimer();
        creep.Interval = TimeSpan.FromMilliseconds(400);
        creep.Tick += (_, _) =>
        {
            if (UploadProgress.Progress < 0.9) UploadProgress.Progress += 0.02;
            UploadSizeLabel.Text =
                $"{UploadProgress.Progress * _recorded.Bytes / 1024d / 1024d:0.0} MB " +
                $"of {_recorded.Bytes / 1024d / 1024d:0.0} MB";
        };
        creep.Start();

        try
        {
            await _api.StopRecordingAsync(_sessionId.Value, _recorded.FilePath);
            creep.Stop();
            UploadProgress.Progress = 1;

            await DisplayAlert("Recording uploaded",
                "Transcription has started. It runs in the background and takes roughly as long " +
                "as the recording. The transcript screen is the next one to be built.", "OK");

            await Shell.Current.GoToAsync("//HomePage");
        }
        catch (Exception ex)
        {
            creep.Stop();
            FailedView.IsVisible = true;
            GoTo(Stage.Failed);

            if (ex is ApiException api)
                await DisplayAlert("Upload rejected", api.Message, "OK");
        }
    }

    private async void OnDiscardClicked(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Discard this recording?",
            "The audio will be lost and the consultation will not be documented.",
            "Discard", "Keep it");

        if (!confirmed) return;

        _recorder.Discard();
        await Shell.Current.GoToAsync("//HomePage");
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        // Uploading is the one state with no way out. The request cannot
        // survive leaving the screen.
        if (_stage == Stage.Uploading)
        {
            await DisplayAlert("Upload in progress",
                "Wait for the upload to finish. Leaving now would lose the recording.", "OK");
            return;
        }

        if (_stage is Stage.Recording or Stage.Selected)
        {
            var confirmed = await DisplayAlert(
                "Discard this consultation?",
                "The recording will be deleted and nothing will be saved.",
                "Discard", "Keep recording");

            if (!confirmed) return;

            _timer?.Stop();
            _recorder.Discard();

            // Close the session the server already knows about, so it does not
            // sit in RECORDING forever. It is not reported by the attention
            // list - with no audio and no transcript there is nothing to
            // resume - so nothing else would ever close it.
            if (_sessionId is { } id)
            {
                try { await _api.DiscardSessionAsync(id); }
                catch { /* an orphaned session is not worth blocking the exit for */ }
            }
        }

        await Shell.Current.GoToAsync("//HomePage");
    }
}
