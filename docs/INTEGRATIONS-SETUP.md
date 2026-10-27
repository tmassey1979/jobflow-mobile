# Integrations setup

## JobFlow Cloud (Supabase)

1. Open the app → **Integrations**.
2. Create an account or sign in with email + password.
3. Users must exist under Supabase **Authentication → Users** (project Owner on the dashboard is not app login).
4. **Jobs → Sync all** loads `shared_job_records` for the signed-in user (RLS enforced).

Never put a Supabase **service-role / secret** key in the Android app. Only the publishable key is embedded.

## Google Calendar (read-only)

JobFlow uses OAuth 2.0 + PKCE. It requests **only** `calendar.readonly`. It never asks for your Google password and does not create or edit events.

### Google Cloud Console

1. Open [Google Cloud Console](https://console.cloud.google.com/).
2. Create or select a project.
3. **APIs & Services → Library** → enable **Google Calendar API**.
4. **APIs & Services → OAuth consent screen**
   - User type: External (or Internal for Workspace-only).
   - App name: JobFlow Mobile.
   - Add scope: `https://www.googleapis.com/auth/calendar.readonly`.
   - Add your Google account as a test user while the app is in Testing.
5. **APIs & Services → Credentials → Create credentials → OAuth client ID**
   - Application type: **Web application** (works with mobile PKCE + custom scheme).
   - Authorized redirect URIs → add exactly:
     ```
     com.tmassey1979.jobflowmobile:/oauth2redirect
     ```
6. Copy the **Client ID** (looks like `….apps.googleusercontent.com`).

### In the app

1. **Integrations** → paste the Client ID into **OAuth client ID**.
2. Tap **Connect Google** → approve access in the browser.
3. Open the **Calendar** tab → Google events appear with local follow-ups.
4. **Disconnect** removes the Google session from the device only.

Optional: set `GoogleCalendarService.DefaultClientId` in code for a fixed client ID so users do not paste it.

### Common failures

| Symptom | Fix |
|--------|-----|
| Needs client ID | Paste Client ID from Google Cloud Credentials |
| Redirect / no code returned | Redirect URI must be exactly `com.tmassey1979.jobflowmobile:/oauth2redirect` |
| Access blocked / app not verified | Add the Google account under OAuth consent **Test users** |
| Calendar empty | Confirm events exist on the primary calendar in the next ~60 days |
| Token expired | Disconnect and Connect again |

### Security

- Scope is read-only only.
- Tokens live in Android Secure Storage.
- No calendar writes in this version.
