# Integrations setup and test plan

## Current implementation

The native Android app exposes a Jobs tab and an Integrations tab.

- **GitHub job feed:** the current supported path is a temporary fine-grained personal access token (PAT) fallback. Restrict it to `tmassey1979/Jobflow-data` and grant Contents: Read-only. The app tests access by loading the latest dated feed before storing the token in Android Secure Storage.
- **Google Calendar:** not configured yet. The UI intentionally does not pretend that OAuth is working and never asks for a Google password.
- **External effects:** this integration slice cannot write to GitHub, submit applications, contact recruiters, or create/update/delete calendar events.

## GitHub native authorization follow-up

Before replacing the PAT fallback:

1. Register/configure the supported GitHub authorization flow for a native Android public client.
2. Use a supported device/browser-based flow and a redirect URI suitable for Android.
3. Do not embed a client secret in the app. A secret shipped in an APK is not confidential.
4. Request only access needed to read the selected private data repository. Keep write access out of the login flow.
5. Store returned credentials only in Android Secure Storage; never log tokens, authorization codes, or HTTP authorization headers.
6. Validate the configured repository and read access after consent. Handle cancellation, expired/revoked credentials, rate limits, missing permissions, and offline state.
7. Record the granted permission names and last successful validation time, not the token itself.

The app registration/client identifier and redirect details must be supplied through an approved configuration process; they must not be guessed or committed as secrets.

## Google Calendar follow-up

1. Create a Google Cloud project and configure the Android OAuth client for the final application ID and signing certificate fingerprints.
2. Enable the Google Calendar API and configure the consent screen and test users.
3. Use Google's supported Android sign-in/authorization flow. Never collect Google passwords.
4. Request the read-only calendar scope only when the user connects Calendar. Do not request event-write scopes in this phase.
5. Let the user select calendars; handle denied consent, multiple accounts, revoked grants, refresh failures, and offline state.
6. Keep all calendar operations read-only until a separately designed feature requests explicit event-write permission and confirms each write.

## Manual acceptance checklist

- [ ] Fresh install can open Integrations and see GitHub disconnected and Calendar not configured.
- [ ] Empty GitHub token shows actionable setup instructions.
- [ ] Valid read-only token is tested against the private feed before being stored.
- [ ] Token input is masked and cleared after a successful connection.
- [ ] Saved credentials are never displayed in the UI or written to logs.
- [ ] Invalid/revoked token produces a recoverable error and does not claim a successful connection.
- [ ] Disconnect asks for confirmation and removes only the saved token from Android Secure Storage.
- [ ] Canceling disconnect leaves the saved token untouched.
- [ ] No GitHub writes, applications, recruiter outreach, or calendar writes occur.
- [ ] Calendar remains visibly unavailable until official OAuth configuration is implemented and tested.

## Automated coverage

`tests/JobFlow.Mobile.IntegrationTests` exercises the provider-neutral state model: connecting, success, failure, reauthentication, disconnect cleanup, permission de-duplication, and sync-time constraints. Device-level secure-storage and provider-consent tests still require Android/emulator testing.
