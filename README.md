# JobFlow Mobile

Private, Android-only JobFlow app built with .NET MAUI and C#.

## What works today

- **Jobs tab** — load the latest `daily/YYYY-MM-DD.json` feed from your chosen private data repo, open HTTPS application links, track a job locally.
- **Applications tab** — search, set stage, notes, and follow-up dates (device-local SQLite only).
- **Integrations tab** — **Sign in with GitHub** (browser device login), choose data repository, sign out.
- **Offline** — last successful feed is cached locally; app still opens without network.

No applications are submitted and nothing is written back to GitHub.

## Install a CI build (phone)

1. Open the latest **green** Android build on `main`:  
   https://github.com/tmassey1979/jobflow-mobile/actions/workflows/android.yml
2. Open the run → **Artifacts** → download **jobflow-mobile-android-apk** (this is a ZIP).
3. On your computer, **unzip** it. Inside you should see **`JobFlow-Mobile.apk`** (not the ZIP itself).
4. Copy `JobFlow-Mobile.apk` to the phone (USB, Drive, etc.).
5. On the phone: open the APK → allow **Install unknown apps** for Files / your browser if asked.

### If install fails

Most common fixes:

| Problem | Fix |
|--------|-----|
| “App not installed” after an older JobFlow build | Uninstall the old **JobFlow** app, then install again (signature / version conflict). |
| Installing the **.zip** | Unzip first; install only `JobFlow-Mobile.apk`. |
| Blocked by Play Protect / unknown apps | Settings → Apps → Special access → Install unknown apps → allow your Files app. |
| `adb` available | `adb uninstall com.tmassey1979.jobflowmobile` then `adb install -r JobFlow-Mobile.apk` |

Package id: `com.tmassey1979.jobflowmobile`  
Min Android: **6.0** (API 23)+

## Run from source

```sh
dotnet workload install maui-android
dotnet restore JobFlow.Mobile.csproj
dotnet publish JobFlow.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk -o artifacts/apk
```

## First-run checklist

1. Confirm your GitHub OAuth App has **Device Flow** enabled.
2. Open the app → **Integrations** → **Sign in with GitHub** → approve in the browser.
3. **Choose data repository** (e.g. `tmassey1979/Jobflow-data`).
4. **Jobs** → **Load latest jobs**.

## CI

GitHub Actions builds the Android target, runs tests, and uploads a single sideload APK named `JobFlow-Mobile.apk`. This is not a Play Store–signed release.
