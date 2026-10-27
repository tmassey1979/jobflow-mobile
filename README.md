# JobFlow Mobile

Private, Android-only JobFlow app built with .NET MAUI and C#.

## What works today

- **Jobs tab** — load the authenticated shared job-listing feed from Supabase, open HTTPS application links, and track jobs locally.
- **Applications tab** — search, set stage, notes, and follow-up dates (device-local SQLite only).
- **Integrations tab** — create/sign in to a Supabase account for cloud feed access.
- **Offline** — last successful feed is cached locally; app still opens without network.

No applications are submitted. The Supabase client uses a publishable key plus the user session and relies on row-level security; never add a service-role key to the Android app. The scheduled feed writer must be configured separately before the cloud feed will contain jobs. Application tracking remains device-local in this version.

## Install the latest release

1. Open [Releases](https://github.com/tmassey1979/jobflow-mobile/releases).
2. Download the single file named `JobFlow-Mobile-vX.Y.Z.apk`.
3. Open that APK on the Android phone and allow your file manager to install unknown apps if prompted.

Each successful push to `main` runs contract and integration tests, builds the Android APK, increments the patch version, and publishes a GitHub Release with the versioned APK. Pull requests and manual runs build test APKs but do not publish releases. The build uploads only the signed APK; the unsigned package is removed to prevent accidentally installing the wrong file.

## One-time release signing setup

A stable signing key is required so Android will accept future APKs as updates to the previously installed release. Do not commit the keystore or passwords to the repository.

Create a release keystore on a trusted computer with a JDK installed:

```powershell
keytool -genkeypair -v -keystore jobflow-release.keystore -alias jobflow -keyalg RSA -keysize 2048 -validity 10000
$bytes = [System.IO.File]::ReadAllBytes("jobflow-release.keystore")
[Convert]::ToBase64String($bytes) | Set-Clipboard
```

In the private repository, open **Settings → Secrets and variables → Actions → New repository secret** and add:

- `ANDROID_SIGNING_KEYSTORE_BASE64` — the Base64 value copied above
- `ANDROID_KEYSTORE_PASSWORD` — the keystore password chosen in keytool
- `ANDROID_KEY_ALIAS` — `jobflow`
- `ANDROID_KEY_PASSWORD` — the key password chosen in keytool

Keep a secure backup of the keystore and passwords. GitHub Actions will use these secrets for signed releases. Until these are configured, CI still builds a signed test APK artifact, but it deliberately blocks publishing a release so updates cannot silently ship with an unstable signing identity.

If you previously installed an APK signed with the temporary CI debug key, uninstall that test build before installing the first stable-signed release. Uninstalling removes local app data.

## First-run checklist

1. Open the app → **Integrations** → create or sign in to your JobFlow Cloud account.
2. Open **Jobs** and tap **Sync all**.
3. Confirm cloud jobs appear on the Jobs tab once the scheduled feed writer is connected.
4. Use **Track** on a job card, then open the Applications tab.

The app reads shared job-listing metadata from Supabase for authenticated accounts. Tailored resume/cover-letter text is kept out of the shared feed and is not yet synced into the app's private kit records. The legacy GitHub account integration has been removed. Recruiter outreach and form submission are not implemented. Application-tracker changes sync to the signed-in user's private Supabase records when cloud access is available; the app keeps the local SQLite copy for offline use.

## Graphics

- Launcher icon: adaptive charcoal background plus blue Signal Mark foreground.
- Splash screen: centered Signal Mark on charcoal.
- In-app branding: the Signal Mark is shown on Jobs, Applications, and Integrations.
- Empty state: the original empty-jobs illustration is reused for Jobs and Applications.
- Runtime graphics are source-controlled SVGs converted by MAUI's asset pipeline; no remote graphics or raster intermediates are required.

## CI

GitHub Actions runs the feed contract tests and offline database/application workflow tests, builds the Android target, and uploads a single installable APK artifact. Main-branch releases include the same versioned APK as a GitHub Release asset.
