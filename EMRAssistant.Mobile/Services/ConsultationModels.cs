using System.Text.Json.Serialization;

namespace EMRAssistant.Mobile.Services;

// ---------------------------------------------------------------- transcript

public record TranscriptSegment(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("speaker_role")] string SpeakerRole,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("start_time")] double? StartTime,
    [property: JsonPropertyName("end_time")] double? EndTime);

public record Transcript(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("session_id")] int SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("segments")] IReadOnlyList<TranscriptSegment> Segments);

/// <summary>
/// Transcript states, as strings for the same reason the attention reasons are
/// strings: an unrecognised value should degrade, not throw.
/// </summary>
public static class TranscriptStatuses
{
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

// ---------------------------------------------------------------- SOAP note

public record SoapSection(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("section_type")] string SectionType,
    [property: JsonPropertyName("content")] string Content);

public record SoapNote(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("session_id")] int SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("sections")] IReadOnlyList<SoapSection> Sections);

public static class SoapSectionTypes
{
    public const string Subjective = "SUBJECTIVE";
    public const string Objective = "OBJECTIVE";
    public const string Assessment = "ASSESSMENT";
    public const string Plan = "PLAN";

    /// <summary>
    /// The clinical order, which is not the order the API returns them in.
    /// S-O-A-P is how a doctor reads a note; anything else looks wrong even if
    /// the content is right.
    /// </summary>
    public static readonly string[] InOrder =
        { Subjective, Objective, Assessment, Plan };

    public static string Title(string type) => type switch
    {
        Subjective => "Subjective",
        Objective => "Objective",
        Assessment => "Assessment",
        Plan => "Plan",
        _ => type,
    };

    public static string Letter(string type) => type switch
    {
        Subjective => "S",
        Objective => "O",
        Assessment => "A",
        Plan => "P",
        _ => "?",
    };
}

public static class SoapNoteStatuses
{
    public const string Draft = "DRAFT";
    public const string Signed = "SIGNED";
}
