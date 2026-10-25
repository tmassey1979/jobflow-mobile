# JobFlow Mobile

Private, Android-only JobFlow app built with .NET MAUI and C#.

## What works today

- **Jobs tab** — connect with a fine-grained GitHub token, load the latest `daily/YYYY-MM-DD.json` feed from `tmassey1979/Jobflow-data`, open HTTPS application links, track a job locally.
- **Applications tab** — search, set stage, notes, and follow-up dates (device-local SQLite only).
- **Integrations tab** — save/test/disconnect the GitHub token (Secure Storage).
- **Offline** — last successful feed is cached locally; app still opens without network.

No applications are submitted and nothing is written back to GitHub.

## Install a CI build (fastest path)

1. Open the latest green **Android build** on `main`:
   https://github.com/tmassey1979/jobflow-mobile/actions/workflows/android.yml
2. Download the **jobflow-mobile-android-apk** artifact.
3. Unzip and install on a device or emulator:

```sh
adb install -r path/to/*.apk
```

On the phone: enable **Install unknown apps** for your file manager / adb if prompted.

## Run from source

Install the .NET 10 SDK and the MAUI Android workload:

```sh
dotnet workload install maui-android
dotnet restore JobFlow.Mobile.csproj
dotnet build JobFlow.Mobile.csproj -f net10.0-android -c Debug
```

Deploy to a connected device/emulator:

```sh
dotnet build JobFlow.Mobile.csproj -f net10.0-android -t:Install -p:AndroidDeviceSerial=<device-id>
# or publish an APK:
dotnet publish JobFlow.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk -o artifacts/apk
adb install -r artifacts/apk/*.apk
```

## First-run checklist

1. Create a **fine-grained** GitHub PAT with **Contents: Read-only** on **`tmassey1979/Jobflow-data` only**.
2. Open the app → **Integrations** (or the Jobs connect card) → paste token → **Connect / Load**.
3. Confirm jobs appear. Token is stored in Android Secure Storage; it is never logged or committed.
4. Use **Track application** on a job card, then open the **Applications** tab.

Revoke the token in GitHub settings when finished testing.

## Private data access

The app reads `daily/*.json` from the private data repository. Writes, recruiter outreach, and form submission are **not** implemented.

## CI

GitHub Actions builds the Android target, runs contract + integration tests, and uploads an APK artifact. Builds verify compilation; they do not sign a Play Store release.
