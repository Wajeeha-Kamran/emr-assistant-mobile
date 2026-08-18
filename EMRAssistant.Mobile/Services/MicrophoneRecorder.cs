using Plugin.Maui.Audio;

namespace EMRAssistant.Mobile.Services;

/// <summary>
/// Live capture from the device microphone.
///
/// This is the implementation the project was always heading for; FilePickerRecorder
/// remains alongside it rather than being replaced, because choosing a file is
/// still the only sane way to exercise the pipeline against the scripted
/// consultation recordings in the backend's evidence folder, and the only way to
/// demonstrate the system without performing a consultation on the spot.
///
/// WAV IS NOT OPTIONAL. The backend loads audio through soundfile, which cannot
/// open M4A or AAC. A recorder that quietly produced compressed audio would
/// upload successfully, pass every check this screen makes, and then fail inside
/// transcription - the worst place to discover it. So the file is verified to be
/// RIFF/WAVE before it is handed back, and StopAsync refuses rather than
/// returning something the server cannot read.
/// </summary>
public class MicrophoneRecorder : IConsultationRecorder
{
    private IAudioRecorder? _recorder;
    private string? _path;

    public bool IsRecording => _recorder?.IsRecording ?? false;

    /// <summary>True. The clock is real and the Finish button means something.</summary>
    public bool CapturesLive => true;

    public bool NeedsPermission => true;

    public async Task<bool> RequestPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();

        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Microphone>();

        return status == PermissionStatus.Granted;
    }

    public async Task StartAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
            throw new InvalidOperationException(
                "Microphone access has not been granted.");

        var recorder = AudioManager.Current.CreateRecorder();

        if (!recorder.CanRecordAudio)
            throw new InvalidOperationException(
                "No microphone is available on this device. You can upload a recording instead.");

        // The cache directory, not app data: an abandoned recording should not
        // outlive the app's own housekeeping. The finished file is uploaded and
        // then deleted by Discard either way.
        _path = Path.Combine(
            FileSystem.CacheDirectory,
            $"consultation_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        // StartAsync() with no argument, then the stream is written out in
        // StopAsync. The overload that takes a file path was added in a later
        // release than this project can use, and this pair has existed in every
        // version of the plugin.
        await recorder.StartAsync();
        _recorder = recorder;
    }

    public async Task<RecordedAudio> StopAsync()
    {
        if (_recorder is null || _path is null)
            throw new InvalidOperationException("Nothing was recorded.");

        var source = await _recorder.StopAsync();
        _recorder = null;

        // Copy what was captured into our own file. Doing it here rather than
        // letting the plugin choose a location keeps the path predictable, and
        // keeps Discard able to delete it.
        await using (var audio = source.GetAudioStream())
        await using (var file = File.Create(_path))
        {
            await audio.CopyToAsync(file);
        }

        var info = new FileInfo(_path);

        if (!info.Exists || info.Length == 0)
            throw new InvalidOperationException(
                "The recording came back empty. Check the microphone is not muted or in use "
                + "by another application.");

        if (!IsWav(_path))
            throw new InvalidOperationException(
                "The recording was not saved as a WAV file, which the server cannot transcribe. "
                + "Upload a WAV recording instead.");

        // Zero here means the header could not be parsed, not that the audio is
        // empty - the length check above already covers that. The screen shows
        // "--:--" and the server enforces the 30-minute limit regardless.
        var duration = FilePickerRecorder.WavDuration(_path);

        return new RecordedAudio(_path, duration, info.Length);
    }

    public void Discard()
    {
        // Stop first if it is still running, or the file stays locked and the
        // delete below fails silently.
        if (_recorder is { IsRecording: true })
        {
            try { _recorder.StopAsync().GetAwaiter().GetResult(); }
            catch { /* discarding: the reason it would not stop does not matter */ }
        }

        _recorder = null;

        if (_path is not null)
        {
            // Unlike a chosen file, this one belongs to the app and nobody else
            // wants it. An abandoned consultation must not leave audio behind.
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch { /* best effort */ }

            _path = null;
        }
    }

    /// <summary>
    /// The first twelve bytes of a WAV file: "RIFF", four bytes of size, "WAVE".
    /// Cheap, and it catches the one failure that would otherwise surface deep
    /// inside the transcription pipeline.
    /// </summary>
    private static bool IsWav(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[12];

            if (stream.Read(header, 0, 12) < 12) return false;

            return header[0] == (byte)'R' && header[1] == (byte)'I'
                && header[2] == (byte)'F' && header[3] == (byte)'F'
                && header[8] == (byte)'W' && header[9] == (byte)'A'
                && header[10] == (byte)'V' && header[11] == (byte)'E';
        }
        catch
        {
            return false;
        }
    }
}
