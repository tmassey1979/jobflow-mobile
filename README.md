# JobFlow Mobile

Private, Android-only JobFlow app built with .NET MAUI and C#.

## What works today

- **Jobs tab** — load the authenticated shared job-listing feed from Supabase, open HTTPS application links, and track jobs locally.
- **Applications tab** — search, set stage, notes, and follow-up dates (device-local SQLite only).
- **Application email preparation** — scan public job/application source pages for published recruiting-related email addresses, then load the matching resume and cover letter directly from Dropbox. Dropbox Paper documents are exported to files on-device; the resume is attached as its actual exported PDF/DOCX file and the cover letter text is placed in the email body.
- **Integrations tab** — create/sign in to a Supabase account for cloud feed access.
- **Settings → Master resume** — import a `.txt`, `.md`, or `.docx` resume, review/edit it, load it from the cloud, and explicitly confirm replacement when saving.
- **Offline** — last successful feed is cached locally; app still opens without network.

JobFlow uses Dropbox OAuth authorization-code with PKCE and requests only `files.content.read`. Dropbox access and refresh tokens are stored in Android SecureStorage. The Dropbox developer app must have the `files.content.read` scope and this redirect URI registered exactly: `com.tmassey1979.jobflowmobile://oauth2redirect`. On the first application-email attempt, tap **Connect Dropbox** and enter the Dropbox app key. The Dropbox app must use Full Dropbox access (not App Folder) so it can read `/JobFlow/Applications`.

No applications are submitted. JobFlow opens the native email composer for review; it never sends automatically. The Supabase client uses a publishable key plus the user session and relies on row-level security; never add a service-role key to the Android app. Application tracking remains device-local in this version.

## Install the latest release

1. Open [Releases](https://github.com/tmassey1979/jobflow-mobile/releases).
2. Download the single file named `JobFlow-Mobile-vX.Y.Z.apk`.
3. Open that APK on the Android phone and allow your file manager to install unknown apps if prompted.

Each successful push to `main` runs contract and integration tests, builds the Android APK, increments the patch version, and publishes a GitHub Release with the versioned APK. Pull requests and manual runs build test APKs but do not publish releases.

## First-run checklist

1. Open the app → **Integrations** → create or sign in to your JobFlow Cloud account.
2. Open **Jobs** and tap **Sync all**.
3. Use **Track** on a job card, then open the Applications tab.
4. Open an application’s email-preparation screen, tap **Connect Dropbox**, and complete Dropbox sign-in.
5. Review the recipient, cover-letter body, and actual resume file attachment in your email app before sending.

## CI

GitHub Actions runs the feed contract tests and offline database/application workflow tests, builds the Android target, and uploads an installable APK artifact.
