namespace EMRAssistant.Mobile.Services;

/// <summary>
/// Where the backend lives, per platform.
///
/// This is not one address. "localhost" means "the device I am running on", so
/// an emulator asking for 127.0.0.1 asks itself, not the laptop. Getting this
/// wrong produces a connection failure that looks like a bug in the app.
///
///   Windows            127.0.0.1     same machine as the API
///   Android emulator   10.0.2.2      the emulator's alias for the host machine
///   Physical phone     LAN address   e.g. 192.168.1.14, same WiFi as the laptop
///
/// run_backend.ps1 in the backend repository prints the emulator and LAN
/// addresses when it starts, so they do not have to be looked up each time.
/// </summary>
public static class ApiConfig
{
#if ANDROID
    // For a PHYSICAL Android device, replace this with the laptop's LAN address
    // printed by run_backend.ps1, and add the firewall rule described in
    // docs/frontend_integration.md. 10.0.2.2 works only for the emulator.
    public const string BaseUrl = "http://10.0.2.2:8000";
#else
    public const string BaseUrl = "http://127.0.0.1:8000";
#endif

    /// <summary>
    /// Generous on purpose. Transcription is not requested synchronously, but
    /// audio upload over a slow link can exceed HttpClient's 100-second default.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
}
