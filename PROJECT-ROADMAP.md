# JobFlow Mobile — Project Roadmap and Implementation Order

**This file is the canonical implementation sequence.** Read it before starting work. GitHub issues are the detailed task specifications; this roadmap establishes dependency order and release gates.

Repository: `tmassey1979/jobflow-mobile`  
App: native Android only, C#/.NET MAUI. No web client or PWA.  
Job data source: private `tmassey1979/Jobflow-data` repository.

## Rules for anyone (including AI coding agents) contributing

1. Read `CLAUDE.md`, this roadmap, `README.md`, and `docs/DATA-CONTRACT.md` before editing.
2. Find the matching GitHub issue and follow its acceptance criteria. If no issue exists, create one before implementing a substantial feature.
3. Work in the phase order below. Do not start downstream work while its prerequisites are broken. If blocked, report the blocker and work only on independent tasks.
4. Before changing code, inspect the current implementation and current GitHub state. Do not assume this roadmap proves a feature is already implemented.
5. Keep commits small and focused. Do not overwrite another contributor's uncommitted or recently changed work. Avoid having two agents edit the same files concurrently; coordinate ownership of files and report the files touched.
6. Never commit tokens, OAuth secrets, signing keys, personal data, or real application documents. Do not log credentials. Use mocks/fixtures for automated tests.
7. Never submit a job application, send an email, write to a calendar, change a remote repository, or share household data without explicit user confirmation at the point of action.
8. Do not silently overwrite local or remote data. Preserve user edits, detect conflicts, and provide preview/confirmation for destructive actions.
9. After each change, run the relevant tests and Android build where available. Report exact commands and whether they passed; never claim an unrun check passed.
10. Update the matching issue with progress, decisions, tests, blockers, and follow-up work. Close issues only when acceptance criteria are met and verified.
11. Preserve the Android-only architecture and the existing data repository contract unless a deliberate, documented migration is approved.
12. If multiple AI agents are working, announce the assigned issue and file ownership first. Prefer parallel work only on independent documentation/tests or clearly separated files.

## Implementation phases (do these in order)

### Phase 0 — Stabilize the current foundation
**Issues:** #28, #5  
- Inspect the existing MAUI app and current Actions results.
- Get a reproducible Android build and establish a test project/CI baseline.
- Inspect real feed examples and schema docs in `Jobflow-data`; align models and parsing to the actual schema.
- Add sanitized fixtures and parser/deduplication tests.
**Exit gate:** clean checkout builds; CI result is known; feed parsing is tested against representative real-shape data.

### Phase 1 — Authentication, integrations hub, and onboarding
**Issues:** #1, #2, #3, #4, #25  
- Design a provider-neutral Integrations screen/state model.
- Implement supported GitHub native authorization without embedding a client secret. Until configured, clearly label any fine-grained read-only PAT entry as a temporary fallback.
- Connect to the configured private data repository with least privilege; keep read access separate from any future write permission.
- Add Google OAuth for Android and start with read-only calendar scopes. Never collect Google passwords.
- Implement first-run setup, repository validation, reconnect/disconnect, permission explanations, expiry/error states, and secure token storage.
**Exit gate:** both provider connection flows can be tested; access and granted permissions are visible; secrets are secure; cancellation and failures recover gracefully.

### Phase 2 — Local-first data layer
**Issues:** #6, #19  
- Introduce SQLite, migrations, repository/data-access abstractions, sync metadata, and offline caching.
- Keep imported source facts separate from user-created notes and edits.
- Define merge/conflict rules before implementing remote writes.
**Exit gate:** jobs and local records survive restarts and work offline; migrations and conflict behavior are tested; failed sync never deletes local work.

### Phase 3 — Core job-search workflows
**Issues:** #7, #8, #9, #16  
- Build search/filtering, stable deduplication, job detail/source verification, application pipeline, timeline, notes, and interview history.
- Preserve source URLs, verification dates, and unknown/unverified values honestly.
**Exit gate:** user can discover/import a job, inspect its sources, track it through stages, and see its full history offline.

### Phase 4 — Documents and responsible AI assistance
**Issues:** #10, #11, #13  
- Add profile/base-resume management and tailored resume/cover-letter generation/export.
- Add claim evidence/factuality checks, versioning, and public recruiter/HR contact sourcing.
- Do not fabricate qualifications or contact information.
**Exit gate:** artifacts can be reviewed and edited before saving; source facts are traceable; exports work; no automatic outreach.

### Phase 5 — Calendar, tasks, and reminders
**Issues:** #14, #15  
- Show selected Google Calendar events and link interviews to applications.
- Add tasks, follow-ups, notification preferences, and reliable scheduling/time-zone behavior.
- Keep calendar integration read-only unless a separately approved, explicit event-write feature is implemented.
**Exit gate:** calendar sync failure does not block the tracker; reminders do not duplicate; no calendar writes occur without explicit confirmation.

### Phase 6 — User-approved application assistance
**Issue:** #12  
- Implement autofill only for supported, documented flows.
- Require a final in-session review and explicit approval before submission.
- Never bypass CAPTCHA, site controls, or employer restrictions.
**Exit gate:** no submission can happen from background automation or generation alone; populated fields and attachments are reviewable.

### Phase 7 — Metrics, preferences, and data portability
**Issues:** #17, #26  
- Add pipeline analytics with documented formulas, configurable search preferences, settings, export, and local-data deletion.
**Exit gate:** metrics handle small/empty data honestly; settings persist; export/delete behavior is clear and confirmed.

### Phase 8 — Backup, restore, and optional household sharing
**Issues:** #18, #19, #20, #25  
- Add encrypted versioned checkpoints, previewable restore timeline, safe GitHub backup/sync, and conflict resolution.
- Add an opt-in household report with field-level selection and preview; private workspace details remain excluded by default.
**Exit gate:** restore is tested and recoverable; writes require confirmation; no sensitive fields are shared by default.

### Phase 9 — UX, release hardening, and delivery
**Issues:** #27, #28, #29  
- Finish native Android navigation, accessibility, loading/offline/error states, security checks, upgrades, and release instructions.
- Produce signed release builds only using protected secrets.
**Exit gate:** CI/tests pass, install/upgrade/restore paths are tested, and a reproducible release artifact is documented.

## Suggested first assignments when multiple agents are used

- **Agent A (integration lane):** Phase 1 design/implementation, primarily integration-specific files; coordinate before editing `MainPage.xaml`, `MainPage.xaml.cs`, `MauiProgram.cs`, or shared auth abstractions.
- **Agent B (data lane):** Phase 0 schema inspection, sanitized fixtures, parser tests, and then Phase 2 database design. Do not edit the same files as Agent A without coordination.
- **Coordinator:** owns shared navigation/app-shell changes, resolves API/model decisions, reviews diffs, runs the full build/tests, and updates this roadmap/issues.

These are suggested lanes, not permission to implement dependent phases out of order.

## Current truth and status discipline

This roadmap is a plan, not evidence that features exist. At the time this document was written, the app has an initial MAUI Android shell and a GitHub feed-reader implementation; authentication currently relies on a manually supplied read-only token. Google Calendar OAuth, SQLite persistence, the complete application tracker, backup/restore, household sharing, and release readiness must be treated as planned until code and tests prove otherwise.

## Definition of done for every issue

- Acceptance criteria met and tested.
- Relevant automated tests added or updated.
- Android build run when feasible, with actual result recorded.
- No secrets or user data committed.
- No unapproved external side effects.
- Documentation and issue status updated.
- Any limitations and manual provider configuration steps clearly recorded.
