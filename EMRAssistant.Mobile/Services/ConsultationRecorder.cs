namespace EMRAssistant.Mobile.Services;

/// <summary>A finished recording, ready to upload.</summary>
public record RecordedAudio(string FilePath, TimeSpan Duration, long Bytes);

/// <summary>
/// Captures the consultation audio.
///
/// An interface rather than a concrete recorder, for the same reason the
/// backend puts Whisper behind an ASREngine protocol: the capture mechanism is
/// the part most likely to change, and the screen should not have to change
/// with it.
///
/// There is deliberately no Pause. The backend has no concept of one, exactly
/// one audio file may exist per session, and a pause would leave an
/// untraceable gap in the clinical record.
/// </summary>
public interface IConsultationRecorder
{
    bool IsRecording { get; }

    /// <summary>
    /// True when audio is captured live, false when a finished file is supplied.
    ///
    /// The screen must know the difference. A live recording has a running
    /// clock and a Finish button; a supplied file is already complete, and
    /// showing RECORDING over a ticking timer while nothing is being captured
    /// would be a plain lie to the person holding the phone.
    /// </summary>
    bool CapturesLive { get; }

    /// <summary>True when the platform must be asked before capture can start.</summary>
    bool NeedsPermission { get; }

    Task<bool> RequestPermissionAsync();

    Task StartAsync();

    /// <summary>Stops and returns the file. Never returns partial results.</summary>
    Task<RecordedAudio> StopAsync();

    /// <summary>Abandons the recording and deletes anything captured.</summary>
    void Discard();
}


/// <summary>
/// The interim recorder: the doctor chooses an existing WAV file instead of
/// capturing live audio.
///
/// Live microphone capture in MAUI needs a third-party package, and adding one
/// is a decision with its own consequences (see the note in RecordPage). This
/// implementation uses FilePicker, which is part of MAUI Essentials and needs
/// nothing extra — and it makes the whole pipeline exercisable today with the
/// scripted consultation recordings already in the backend's evidence folder.
///
/// Swapping in a microphone recorder later means writing one class and changing
/// one registration in MauiProgram. No screen code changes.
/// </summary>
public class FilePickerRecorder : IConsultationRecorder
{
    private string? _path;

    public bool IsRecording { get; private set; }

    /// <summary>False. The file already exists and has a fixed length.</summary>
    public bool CapturesLive => false;

    // FilePicker needs no microphone permission. A live recorder would.
    public bool NeedsPermission => false;

    public Task<bool> RequestPermissionAsync() => Task.FromResult(true);

    public async Task StartAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a consultation recording (WAV)",
        });

        if (result is null)
            throw new OperationCanceledException("No file was chosen.");

        _path = result.FullPath;
        // Not "recording" - the audio is already complete. The flag only means
        // a file is held and waiting to be uploaded.
        IsRecording = true;
    }

    public Task<RecordedAudio> StopAsync()
    {
        if (_path is null)
            throw new InvalidOperationException("Nothing was recorded.");

        IsRecording = false;

        var info = new FileInfo(_path);
        return Task.FromResult(new RecordedAudio(_path, WavDuration(_path), info.Length));
    }

    public void Discard()
    {
        IsRecording = false;
        // The file belongs to the user, so it is not deleted - only forgotten.
        _path = null;
    }

    /// <summary>
    /// Reads a WAV file's duration from its header.
    ///
    /// Worth doing rather than guessing from file size: the backend rejects
    /// anything over 30 minutes, and it does so AFTER the upload completes. A
    /// client that knows the duration can refuse before spending the upload.
    ///
    /// Walks the RIFF chunks rather than assuming fmt is at a fixed offset,
    /// because real files carry LIST and fact chunks in between.
    /// </summary>
    public static TimeSpan WavDuration(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (new string(reader.ReadChars(4)) != "RIFF") return TimeSpan.Zero;
            reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE") return TimeSpan.Zero;

            int byteRate = 0;

            while (stream.Position < stream.Length - 8)
            {
                var id = new string(reader.ReadChars(4));
                var size = reader.ReadUInt32();
                var next = stream.Position + size + (size % 2);

                if (id == "fmt ")
                {
                    reader.ReadUInt16();                  // audio format
                    reader.ReadUInt16();                  // channels
                    reader.ReadUInt32();                  // sample rate
                    byteRate = (int)reader.ReadUInt32();  // bytes per second
                }
                else if (id == "data" && byteRate > 0)
                {
                    return TimeSpan.FromSeconds((double)size / byteRate);
                }

                if (next >= stream.Length) break;
                stream.Position = next;
            }
        }
        catch
        {
            // A duration we cannot read is not worth crashing the screen for.
            // The server enforces the limit regardless.
        }

        return TimeSpan.Zero;
    }
}
