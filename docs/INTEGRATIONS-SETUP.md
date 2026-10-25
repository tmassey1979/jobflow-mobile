# Integrations setup

## GitHub sign-in (device login)

JobFlow does **not** ask you to paste a personal access token.

### One-time OAuth App (you own this)

1. Open [GitHub Developer Settings → OAuth Apps](https://github.com/settings/developers) → **New OAuth App**.
2. Application name: `JobFlow Mobile` (or similar).
3. Homepage URL: `https://github.com/tmassey1979/jobflow-mobile`
4. Authorization callback URL: `http://localhost` (required by GitHub; device login does not use it).
5. Create the app, then open it and **enable Device Flow**.
6. Copy the **Client ID** only. Do **not** put a client secret in the app.

### In the app

1. Open **Integrations**.
2. Paste the public **Client ID** once (stored on device).
3. Tap **Sign in with GitHub**.
4. GitHub shows a short code in the app; the browser opens `github.com/login/device`.
5. Enter the code and approve access.
6. When the app detects approval, choose your **data repository** (for example `tmassey1979/Jobflow-data`).
7. JobFlow tests a read of `daily/YYYY-MM-DD.json` in that repository.

### Scopes

Classic OAuth Apps need the `repo` scope to read **private** repository contents. JobFlow only calls the repository you select afterward (list + read feed). It does not write to GitHub, submit applications, or contact employers.

### Sign out

**Sign out of GitHub** clears the access token and selected repository from Secure Storage on this device.

## Google Calendar

Not configured yet.
