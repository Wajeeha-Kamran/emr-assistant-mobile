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
    [property: JsonPropertyName("sections")] IReadOnlyList<SoapSection> Sections,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    // Progress, which is a different axis from Status. Status is DRAFT or
    // SIGNED — where the note is in its life. These say whether the machine has
    // finished writing it. Sections is empty while GenerationStatus is
    // processing, so the two must be read together.
    [property: JsonPropertyName("generation_status")] string GenerationStatus = GenerationStatuses.Completed,
    [property: JsonPropertyName("generation_error")] string? GenerationError = null,
    [property: JsonPropertyName("codes_generation_status")] string? CodesGenerationStatus = null,
    [property: JsonPropertyName("codes_generation_error")] string? CodesGenerationError = null);

/// <summary>
/// Generation progress, as strings for the same reason every other status here
/// is a string: an unrecognised value should degrade, not throw.
///
/// GenerationStatus defaults to Completed when the field is absent, so a build
/// of this app pointed at a backend that predates the asynchronous endpoints
/// behaves exactly as it used to instead of polling forever for a field that
/// never arrives.
/// </summary>
public static class GenerationStatuses
{
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

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

// ---------------------------------------------------------------- codes

public record CodeSuggestion(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("code_type")] string CodeType,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
    [property: JsonPropertyName("accepted")] bool Accepted);

public static class CodeTypes
{
    public const string Icd10 = "ICD10";
    public const string Cpt = "CPT";

    /// <summary>What the group is called on screen, and where it came from.</summary>
    public static string Title(string type) => type switch
    {
        Icd10 => "Diagnosis \u00b7 ICD-10",
        Cpt => "Procedures \u00b7 CPT",
        _ => type,
    };

    public static string Source(string type) => type switch
    {
        Icd10 => "From the Assessment section",
        Cpt => "From the Plan section",
        _ => "",
    };
}

// ---------------------------------------------------------------- signing

public record Signature(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("soap_note_id")] int SoapNoteId,
    [property: JsonPropertyName("doctor_id")] int DoctorId,
    [property: JsonPropertyName("signed_at")] DateTimeOffset SignedAt,
    [property: JsonPropertyName("method")] string Method);

// SyncStatuses is not declared here. It already exists in AttentionModels.cs,
// where the dashboard's attention list needed it first, and the values are the
// same three the sign screen polls for.
