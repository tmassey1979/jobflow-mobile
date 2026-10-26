# Integrations setup

## GitHub sign-in (device login)

JobFlow does **not** ask you to paste a personal access token.

### OAuth App checklist (required)

1. Open [GitHub → Settings → Developer settings → OAuth Apps](https://github.com/settings/developers)
2. Open the **JobFlow Mobile** app (Client ID starts with `Ov23li…`)
3. Turn **ON** **Device Flow**
4. Do **not** put a client secret in the mobile app

If Device Flow is off, sign-in fails immediately with a clear error in the app.

### In the app

1. **Integrations** → **Sign in with GitHub**
2. A one-time code appears (and is copied when possible)
3. Browser opens `github.com/login/device` (code may be prefilled)
4. Approve access on GitHub
5. Return to JobFlow and **keep the Integrations screen open** until it says signed in
6. **Choose data repository** (for example `tmassey1979/Jobflow-data`)
7. **Jobs** → Load latest jobs

If Android closes JobFlow while you are in the browser, reopen the app — it will try to finish a still-valid pending approval.

### Common failures

| Symptom | Fix |
|--------|-----|
| Sign-in fails immediately | Enable **Device Flow** on the OAuth App |
| Code expires | Tap Sign in again and approve within ~15 minutes |
| Denied | Approve the permission prompt on GitHub |
| Repo list empty | Sign in with the account that owns `Jobflow-data` |
| Feed issue after repo pick | Confirm `daily/YYYY-MM-DD.json` exists in that repo |

### Scopes

Classic OAuth Apps need the `repo` scope to read **private** repository contents. JobFlow only reads the repository you select afterward.

## Google Calendar

Not configured yet.
