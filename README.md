# EMR Assistant — Mobile client

.NET MAUI client for the AI-Powered EMR Assistant backend.

Final-year project, Quaid-e-Azam University.
Wajeeha Kamran · Supervisor: Dr. Ayyaz Hussain

## Requirements

- Visual Studio 2022 with the **.NET Multi-platform App UI development** workload
- The backend running — see the `emr-assistant-backend` repository

## Running

Start the backend first, from its repository root:

```
.\run_backend.ps1
```

Then open `EMRAssistant.Mobile.sln`, choose **Windows Machine** in the target
dropdown, and press F5.

Android requires the Android SDK, which is not yet installed on the development
machine. Windows is the current development target; the same code builds for
Android once that tooling is in place.

## Where the backend lives

`Services/ApiConfig.cs` selects the address by platform, because there is no
single one that works everywhere:

| Target | Address | Why |
|---|---|---|
| Windows | `http://127.0.0.1:8000` | Same machine as the API |
| Android emulator | `http://10.0.2.2:8000` | The emulator's alias for the host; `127.0.0.1` would mean the emulator itself |
| Physical phone | the laptop's LAN address | Both devices on the same WiFi, and a firewall rule is required |

The sign-in screen displays the address it is calling, which turns "it doesn't
work" into "it is calling the wrong host".

## Structure

```
Services/
  ApiConfig.cs    where the backend is, per platform
  ApiClient.cs    all HTTP calls, token storage, error translation
Views/
  LoginPage       sign in or register; proves connectivity, auth and token use
```

`ApiClient` is registered as a singleton so one `HttpClient` is shared for the
life of the app. The access token is kept in `SecureStorage`, not `Preferences` —
`Preferences` is plain text on disk, and the token grants access to clinical
records.

## Notes for building further screens

- **Transcription is asynchronous.** `stop-recording` returns immediately; the
  transcript takes roughly as long as the recording. The client must poll and
  show an honest waiting state.
- **404 can mean "belongs to another doctor".** The API returns it rather than
  403 so it does not confirm that other people's records exist. Do not tell the
  user a record was deleted.
- **The SOAP note is a draft for clinical review.** Editing should feel expected,
  and signing should require deliberate confirmation, because it is irreversible.

Full integration detail: `docs/frontend_integration.md` in the backend repository.
