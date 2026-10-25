# Integrations setup

## GitHub sign-in (device login)

JobFlow does **not** ask you to paste a personal access token.

### OAuth App

This build ships with the public Client ID for the JobFlow Mobile OAuth App.
Device Flow must stay enabled on that app:

1. [GitHub → Settings → Developer settings → OAuth Apps](https://github.com/settings/developers)
2. Open the JobFlow Mobile app → **Enable Device Flow**
3. Client ID is public (already in the app). Do **not** put a client secret in the app.

### In the app

1. Open **Integrations**.
2. Tap **Sign in with GitHub** (Client ID is prefilled).
3. GitHub shows a short code; the browser opens `github.com/login/device`.
4. Enter the code and approve access.
5. Choose your **data repository** (for example `tmassey1979/Jobflow-data`).
6. JobFlow tests a read of `daily/YYYY-MM-DD.json` in that repository.

### Scopes

Classic OAuth Apps need the `repo` scope to read **private** repository contents. JobFlow only calls the repository you select afterward. It does not write to GitHub, submit applications, or contact employers.

### Sign out

**Sign out of GitHub** clears the access token and selected repository from Secure Storage on this device.

## Google Calendar

Not configured yet.
