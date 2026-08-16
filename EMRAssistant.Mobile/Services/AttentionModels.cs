using System.Text.Json.Serialization;

namespace EMRAssistant.Mobile.Services;

/// <summary>
/// One consultation that did not finish.
///
/// Reason and Action are strings rather than enums on purpose. A C# enum throws
/// on a value it has not seen, so adding a reason to the backend would crash
/// every installed copy of the app the first time one appeared. A string is
/// forwards-compatible: an unrecognised reason can be shown generically instead
/// of taking the screen down.
/// </summary>
public record AttentionItem(
    [property: JsonPropertyName("session_id")] int SessionId,
    [property: JsonPropertyName("note_id")] int? NoteId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_edited_at")] DateTimeOffset? LastEditedAt);

public record AttentionList(
    [property: JsonPropertyName("items")] IReadOnlyList<AttentionItem> Items,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("counts")] IReadOnlyDictionary<string, int> Counts)
{
    public static readonly AttentionList Empty =
        new(Array.Empty<AttentionItem>(), 0, new Dictionary<string, int>());
}

/// <summary>
/// The stages a consultation can be stuck at. One per stage of
/// record -> transcribe -> generate note -> sign -> sync.
/// </summary>
public static class AttentionReasons
{
    public const string TranscriptFailed = "TRANSCRIPT_FAILED";
    public const string TranscriptStalled = "TRANSCRIPT_STALLED";
    public const string NoteNotGenerated = "NOTE_NOT_GENERATED";
    public const string NotSigned = "NOT_SIGNED";
    public const string SyncFailed = "SYNC_FAILED";
}

/// <summary>
/// What to offer for a stuck consultation. Read this rather than switching on
/// the reason: the backend decides the recovery path so the client does not
/// have to keep a duplicate copy of the rule in step with it.
/// </summary>
public static class AttentionActions
{
    public const string ResumeTranscription = "RESUME_TRANSCRIPTION";
    public const string GenerateNote = "GENERATE_NOTE";
    public const string SignNote = "SIGN_NOTE";
    public const string RetrySync = "RETRY_SYNC";
}

/// <summary>Sync states reported by the API.</summary>
public static class SyncStatuses
{
    public const string Pending = "PENDING";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
}
