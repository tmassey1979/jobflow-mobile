# JobFlow Mobile

Private, Android-only JobFlow app built with .NET MAUI and C#.

## Current scope

- Android target only; no web client or PWA.
- Read the latest dated job feed from the private `tmassey1979/Jobflow-data` repository.
- Cache the selected GitHub token using Android Secure Storage and use it only for read requests to the configured data repository.
- Display jobs from the feed and open official application links.
- Keep the app usable as a starting point for an offline-first SQLite workspace. Local persistence and richer application tracking are planned next.

## Run locally

Install the .NET 10 SDK and the MAUI Android workload, then run:

```sh
dotnet workload install maui-android
dotnet build JobFlow.Mobile.csproj -f net10.0-android
```

Open the project in Visual Studio with the .NET MAUI workload for device/emulator deployment.

## Private data access

Create a fine-grained GitHub personal access token with **read-only Contents access** for `tmassey1979/Jobflow-data` only. Enter it inside the app. The token is stored through platform Secure Storage, never in source code or app configuration. Do not paste tokens into issues, chat, logs, or commits. Revoke the token from GitHub settings if it is no longer needed.

The app reads `daily/*.json` and then the feed's relative metadata/document paths from the private data repository. Writes are not implemented in this first slice. No applications are submitted and no recruiters are contacted.

## Reusing the original prototype

The original `tmassey1979/jobflow` repository is a web prototype. This repository starts a clean Android-specific implementation, retaining compatible domain concepts and the existing data contract rather than carrying over the web runtime and web release pipeline.

## CI

The GitHub Actions workflow builds the Android project and uploads an APK artifact. This verifies compilation; it does not yet sign a production release or publish to Google Play.
