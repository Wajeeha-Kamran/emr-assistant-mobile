using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EMRAssistant.Mobile.Services;

/// <summary>
/// Thrown when the API refuses a request. Carries the status code so callers can
/// distinguish "wrong password" from "server is down" without parsing strings.
/// </summary>
public class ApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public ApiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner) => StatusCode = statusCode;
}

public record Doctor(int Id, string Email, string FullName);

/// <summary>
/// Talks to the EMR Assistant backend.
///
/// Registered as a singleton in MauiProgram so one HttpClient is shared for the
/// life of the app. Creating an HttpClient per request exhausts sockets under
/// load -- a well-known .NET trap, and worth avoiding from the first screen.
/// </summary>
public class ApiClient
{
    private const string TokenKey = "emr_access_token";

    private readonly HttpClient _http;
    private string? _token;

    public ApiClient()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl),
            Timeout = ApiConfig.Timeout,
        };
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    // -- authentication ----------------------------------------------------

    /// <summary>
    /// Log in and remember the token.
    ///
    /// The endpoint expects FORM encoding, not JSON -- it follows the OAuth2
    /// password flow so the interactive API docs can authorise directly. The
    /// "username" field takes the doctor's EMAIL address.
    /// </summary>
    /// <param name="rememberMe">
    /// When false the token is kept in memory only, so closing the app signs the
    /// user out. On a shared or clinic device that is the safer default, which is
    /// why it is a real setting rather than decoration.
    /// </param>
    public async Task LoginAsync(string email, string password, bool rememberMe = true)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", email),
            new KeyValuePair<string, string>("password", password),
        });

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/api/v1/auth/login", form);
        }
        catch (Exception ex)
        {
            // A connection failure here is almost always the base URL or the
            // backend not running -- say so, rather than surfacing a raw
            // socket error the user cannot act on.
            throw new ApiException(
                $"Could not reach the server at {ApiConfig.BaseUrl}. " +
                "Check the backend is running (run_backend.ps1) and that the " +
                "address is right for this platform.", null, ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new ApiException("Email or password is incorrect.", response.StatusCode);

        await ThrowIfFailedAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("access_token").GetString();

        if (string.IsNullOrEmpty(token))
            throw new ApiException("The server returned no access token.");

        _token = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (rememberMe)
        {
            // SecureStorage, not Preferences. Preferences is plain text on disk,
            // and this token grants access to clinical records.
            try { await SecureStorage.Default.SetAsync(TokenKey, token); }
            catch { /* unavailable on some platforms; the in-memory token still works */ }
        }
        else
        {
            // Clear anything a previous "keep me signed in" left behind, or the
            // checkbox would have no effect on the second sign-in.
            try { SecureStorage.Default.Remove(TokenKey); } catch { }
        }
    }

    public async Task RegisterAsync(string email, string password, string fullName)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            full_name = fullName,
        });

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ApiException("An account with that email already exists.", response.StatusCode);

        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Restore a token saved on a previous run, so the user is not asked to log
    /// in every time. Returns false if there is nothing stored.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            if (string.IsNullOrEmpty(token)) return false;

            _token = token;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Logout()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
        try { SecureStorage.Default.Remove(TokenKey); } catch { }
    }

    /// <summary>
    /// Who is logged in. Also the cheapest way to prove a token actually works:
    /// obtaining a token and using one successfully are different things.
    /// </summary>
    public async Task<Doctor> GetCurrentDoctorAsync()
    {
        var response = await _http.GetAsync("/api/v1/auth/me");
        await ThrowIfFailedAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Doctor(
            payload.GetProperty("id").GetInt32(),
            payload.GetProperty("email").GetString() ?? "",
            payload.TryGetProperty("full_name", out var name) ? name.GetString() ?? "" : "");
    }

    // -- consultations -----------------------------------------------------

    /// <summary>
    /// Create a consultation session. Returns its id, which every later call in
    /// the flow is keyed by.
    /// </summary>
    public async Task<int> CreateSessionAsync()
    {
        // The trailing slash is required: the route is declared as "/" under the
        // "/api/v1/sessions" prefix. Without it the request is redirected, and a
        // redirect drops the Authorization header on some platforms.
        var response = await _http.PostAsync("/api/v1/sessions/", null);
        await ThrowIfFailedAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetInt32();
    }

    /// <summary>INITIATED -> RECORDING. Called the moment the doctor presses Start.</summary>
    public async Task StartRecordingAsync(int sessionId)
    {
        var response = await _http.PostAsync($"/api/v1/sessions/{sessionId}/start-recording", null);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Uploads the audio and ends the recording.
    ///
    /// Returns once the file is stored and validated; transcription then runs
    /// in the background. The 30-minute limit is enforced here, server-side,
    /// AFTER the upload completes, which is why the client stops at 30:00 by
    /// itself rather than relying on this.
    /// </summary>
    public async Task StopRecordingAsync(int sessionId, string filePath)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        var file = new StreamContent(stream);

        // audio/wav rather than the .wav extension's platform-dependent guess.
        // The API accepts every spelling, but being explicit avoids relying on
        // that.
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", Path.GetFileName(filePath));

        var response = await _http.PostAsync($"/api/v1/sessions/{sessionId}/stop-recording", content);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Closes a consultation abandoned before any audio was uploaded.
    ///
    /// Returns 409 once the session is STOPPED: by then the recording exists
    /// and the consultation holds clinical content, so it must be completed or
    /// recovered rather than abandoned.
    /// </summary>
    public async Task DiscardSessionAsync(int sessionId)
    {
        var response = await _http.PostAsync($"/api/v1/sessions/{sessionId}/discard", null);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// The transcript and its speaker-labelled segments.
    ///
    /// Poll this while status is "processing". Transcription runs in the
    /// background and is slower than real time on CPU, so a nine-minute
    /// consultation takes several minutes.
    /// </summary>
    public async Task<Transcript> GetTranscriptAsync(int sessionId)
    {
        var response = await _http.GetAsync($"/api/v1/sessions/{sessionId}/transcript");
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<Transcript>()
               ?? throw new ApiException("The transcript could not be read.");
    }

    /// <summary>The note and its four sections. 404 until it has been generated.</summary>
    public async Task<SoapNote> GetSoapNoteAsync(int sessionId)
    {
        var response = await _http.GetAsync($"/api/v1/sessions/{sessionId}/soap-notes");
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<SoapNote>()
               ?? throw new ApiException("The note could not be read.");
    }

    /// <summary>
    /// Generate the draft. Returns the note.
    ///
    /// Slow: the NLP runs inline rather than in the background, so this request
    /// can take 15 to 25 seconds. Refused if the transcript is not complete, or
    /// if the note has already been signed.
    /// </summary>
    public async Task<SoapNote> GenerateSoapNoteAsync(int sessionId)
    {
        var response = await _http.PostAsync($"/api/v1/sessions/{sessionId}/soap-notes/generate", null);
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<SoapNote>()
               ?? throw new ApiException("The note could not be read.");
    }

    /// <summary>
    /// Edit one section. Refused once the note is signed — that is the point of
    /// signing.
    /// </summary>
    public async Task<SoapNote> UpdateSectionAsync(int noteId, int sectionId, string content)
    {
        var response = await _http.PatchAsJsonAsync(
            $"/api/v1/soap-notes/{noteId}/sections/{sectionId}", new { content });
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<SoapNote>()
               ?? throw new ApiException("The note could not be read.");
    }

    // -- recovering consultations that did not finish ----------------------

    /// <summary>
    /// The consultations that are stuck, and what to do about each.
    ///
    /// Empty under normal use. This is the only endpoint that enumerates a
    /// doctor's consultations, so without it an interrupted one cannot be
    /// reached from the app at all.
    /// </summary>
    public async Task<AttentionList> GetAttentionAsync()
    {
        var response = await _http.GetAsync("/api/v1/attention");
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<AttentionList>()
               ?? AttentionList.Empty;
    }

    /// <summary>
    /// Re-run transcription for a session whose transcript failed or was
    /// abandoned. Any segments already produced are replaced.
    /// </summary>
    public async Task RetryTranscriptionAsync(int sessionId)
    {
        var response = await _http.PostAsync($"/api/v1/sessions/{sessionId}/transcript/retry", null);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Generate the SOAP draft for a session whose transcript is ready.
    /// Slow -- the NLP pipeline runs inline, not in the background.
    /// </summary>
    public async Task GenerateNoteAsync(int sessionId)
    {
        var response = await _http.PostAsync($"/api/v1/sessions/{sessionId}/soap-notes/generate", null);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Re-queue a failed push to the EMR.
    ///
    /// Returns once the job is QUEUED, not once it has succeeded -- poll
    /// <see cref="GetSyncStatusAsync"/> for the outcome. A 409 means the note is
    /// not in a failed state, usually because a sync is already in flight.
    /// </summary>
    public async Task RetrySyncAsync(int noteId)
    {
        var response = await _http.PostAsync($"/api/v1/soap-notes/{noteId}/retry-sync", null);
        await ThrowIfFailedAsync(response);
    }

    /// <summary>
    /// Current sync state: PENDING, SUCCESS or FAILED. Null before the note is
    /// signed, because nothing has been queued yet.
    /// </summary>
    public async Task<string?> GetSyncStatusAsync(int noteId)
    {
        var response = await _http.GetAsync($"/api/v1/soap-notes/{noteId}/sync-status");
        await ThrowIfFailedAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.TryGetProperty("sync_status", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    // -- billing codes and signing -----------------------------------------

    /// <summary>
    /// Ask for code suggestions. Ranked ICD-10 from the Assessment section and
    /// CPT from the Plan section, five of each.
    ///
    /// Slow, like note generation: the matching runs inline rather than in a
    /// background task, so allow the same 15 to 25 seconds.
    /// </summary>
    public async Task<IReadOnlyList<CodeSuggestion>> GenerateCodeSuggestionsAsync(int noteId)
    {
        var response = await _http.PostAsync(
            $"/api/v1/soap-notes/{noteId}/code-suggestions/generate", null);
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<List<CodeSuggestion>>()
               ?? new List<CodeSuggestion>();
    }

    /// <summary>The suggestions already produced for a note. Empty before generation.</summary>
    public async Task<IReadOnlyList<CodeSuggestion>> GetCodeSuggestionsAsync(int noteId)
    {
        var response = await _http.GetAsync($"/api/v1/soap-notes/{noteId}/code-suggestions");
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<List<CodeSuggestion>>()
               ?? new List<CodeSuggestion>();
    }

    /// <summary>
    /// Accept or remove one suggestion.
    ///
    /// The server stores a plain boolean, so there is no third "not yet looked
    /// at" state -- a code is accepted or it is not, and only accepted codes go
    /// with the note. Refused once the note is signed.
    /// </summary>
    public async Task<CodeSuggestion> UpdateCodeSuggestionAsync(int noteId, int suggestionId, bool accepted)
    {
        var response = await _http.PatchAsJsonAsync(
            $"/api/v1/soap-notes/{noteId}/code-suggestions/{suggestionId}", new { accepted });
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<CodeSuggestion>()
               ?? throw new ApiException("The suggestion could not be read.");
    }

    /// <summary>
    /// Sign the note. Irreversible.
    ///
    /// Three things happen: the note is locked against editing, the session is
    /// finalised so its audio becomes eligible for deletion, and the push to the
    /// EMR is queued in the background. The signature returns immediately; the
    /// sync does not, so poll <see cref="GetSyncStatusAsync"/> afterwards.
    ///
    /// A 409 means the note was already signed -- treat that as success and show
    /// the sync state, not as an error.
    /// </summary>
    public async Task<Signature> SignNoteAsync(int noteId)
    {
        var response = await _http.PostAsync($"/api/v1/soap-notes/{noteId}/sign", null);
        await ThrowIfFailedAsync(response);

        return await response.Content.ReadFromJsonAsync<Signature>()
               ?? throw new ApiException("The signature could not be read.");
    }

    // -- shared error handling ---------------------------------------------

    /// <summary>
    /// Turn a failed response into an ApiException carrying the API's own
    /// message. FastAPI puts a readable explanation in "detail"; surfacing it
    /// is far more useful than "the request failed".
    /// </summary>
    private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string detail;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            detail = body.TryGetProperty("detail", out var d)
                ? d.ValueKind == JsonValueKind.String ? d.GetString()! : d.ToString()
                : response.ReasonPhrase ?? "Request failed";
        }
        catch
        {
            detail = response.ReasonPhrase ?? "Request failed";
        }

        // 404 can mean "belongs to another doctor". The API returns it instead of
        // 403 deliberately, so it does not confirm that another doctor's records
        // exist. Do not tell the user something was deleted.
        throw new ApiException(detail, response.StatusCode);
    }
}
