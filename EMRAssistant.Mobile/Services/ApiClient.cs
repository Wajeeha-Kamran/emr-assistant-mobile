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
    public async Task LoginAsync(string email, string password)
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

        // SecureStorage, not Preferences. Preferences is plain text on disk, and
        // this token grants access to clinical records.
        try { await SecureStorage.Default.SetAsync(TokenKey, token); }
        catch { /* unavailable on some platforms; the in-memory token still works */ }
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
