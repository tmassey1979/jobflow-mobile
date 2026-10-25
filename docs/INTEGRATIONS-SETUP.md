# Integrations setup and test plan

## Current implementation

The native Android app exposes Jobs, Applications, and Integrations tabs.

- **GitHub job feed:** temporary fine-grained PAT fallback for `tmassey1979/Jobflow-data` with Contents: Read-only. Access is tested by loading the latest dated feed before Secure Storage. The app does not introspect token scopes.
- **Google Calendar:** not configured; no password collection or pretend OAuth.
- **Application tracker:** local-only SQLite; no auto-submit or outreach.
- **Offline cache:** last validated feed only; never stores credentials.

## Safety

No GitHub writes, applications, recruiter outreach, or calendar writes in this slice.
