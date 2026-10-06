namespace EMRAssistant.Mobile.Services;

/// <summary>
/// Where the backend lives.
///
/// "localhost" means "the device I am running on", so a phone asking for
/// 127.0.0.1 normally asks itself, not the laptop. The exception is when the
/// USB cable is carrying that port, which is what adb reverse arranges:
///
///     D:\platform-tools\adb.exe reverse tcp:8000 tcp:8000
///
/// After that, port 8000 on the phone IS port 8000 on the laptop, down the
/// cable. One address then works for a physical phone, the emulator and
/// Windows alike, so there is nothing to edit when the target changes.
///
/// WHY NOT THE LAN ADDRESS
/// http://192.168.x.x:8000 also works, and docs/frontend_integration.md in the
/// backend repository describes it. It needs an inbound firewall rule, both
/// devices on the same WiFi, and a new address every time the network changes
/// -- three things that can fail in a room on the day of a demonstration. A
/// cable cannot be on the wrong network. If the cable is ever unavailable,
/// that route is still there: put the laptop's LAN address here instead and
/// add the firewall rule.
///
/// ADB REVERSE IS NOT PERMANENT. It is cleared when the cable is unplugged,
/// the phone reboots, or the adb server restarts. Re-run the one command;
/// nothing else needs redoing. If the app suddenly cannot reach the backend
/// and nothing else changed, this is the first thing to check:
///
///     D:\platform-tools\adb.exe reverse --list
///
/// WITHOUT ADB REVERSE, on the emulator only, the address is 10.0.2.2:8000 --
/// the emulator's built-in alias for its host machine. It means nothing to a
/// real phone, which is why this file no longer uses it.
///
/// Android also refuses plain HTTP unless AndroidManifest.xml sets
/// usesCleartextTraffic, which it does. Without that every address fails
/// identically and the address looks like the culprit when it is not.
/// </summary>
public static class ApiConfig
{
#if ANDROID
    public const string BaseUrl = "http://127.0.0.1:8000";
#else
    public const string BaseUrl = "http://127.0.0.1:8000";
#endif

    /// <summary>
    /// Generous on purpose. Transcription is not requested synchronously, but
    /// audio upload over a slow link can exceed HttpClient's 100-second default.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
}
