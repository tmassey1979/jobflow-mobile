# Claude Code Instructions — JobFlow Mobile

Read this file first, then read `PROJECT-ROADMAP.md`, `README.md`, and `docs/DATA-CONTRACT.md` before making changes.

## What this repository is

JobFlow Mobile is a **native Android-only app written in C#/.NET MAUI**. Do not introduce a web client, React frontend, or PWA here. It reads job feeds from the private GitHub repository `tmassey1979/Jobflow-data`. The long-term product includes offline-first job tracking, application kits, Google Calendar integration, safe backup/restore, and opt-in household progress reporting.

## Required workflow

1. Check the current branch, working tree, recent commits, open GitHub issues, and CI state before coding. Never assume another agent's work is absent.
2. Read `PROJECT-ROADMAP.md` and follow its phase order and exit gates. GitHub issues define the detailed scope; the roadmap defines sequencing.
3. Pick one issue (or a clearly bounded part of an issue), state the issue number and files you intend to own, and avoid concurrent edits to the same files. If another agent may be working on those files, coordinate before editing.
4. Inspect existing code and conventions before designing a new abstraction. Prefer small, incremental changes that fit the current MAUI/C# architecture.
5. Add or update tests. Run the narrow tests first, then the Android build when feasible. Record the actual command and result. Never say a build passed unless it ran successfully.
6. Update the matching GitHub issue with implementation details, tests, risks, and blockers. Do not close it until its acceptance criteria are demonstrably satisfied.
7. Summarize the files changed, behavior changed, test/build results, issue updated, and any manual setup still required.

## Product and security invariants

- Android-only. No web frontend/PWA.
- GitHub feed access must use least privilege. Keep read-only feed access separate from optional write-enabled backup/sync.
- Use supported OAuth/device authorization flows. Never embed a client secret in the mobile app; never collect a Google password. Any temporary personal-access-token fallback must be explicitly documented as temporary and use the narrowest repository permissions.
- Store tokens/keys only in Android secure storage or an appropriate secure mechanism. Never commit secrets or log tokens, authorization headers, personal data, or real application documents.
- Google Calendar starts read-only. Any future event creation/update needs a separate preview and explicit confirmation.
- Never automatically submit job applications, send emails, contact recruiters, write remote repository data, or share household data. Require explicit user approval at the moment of the external action.
- Do not fabricate job facts, qualifications, recruiter contacts, salaries, remote eligibility, or verification.
- Keep each user's private workspace independent. Household reports are opt-in, field-selectable, previewed, and exclude sensitive data by default.
- Preserve user edits. Never silently overwrite local/remote data. Design sync and restore with conflict detection, previews, and recovery.
- Keep schemas versioned and compatible; use sanitized fixtures and mocks in tests.

## Collaboration rules

- Do not reset, force-push, revert, or overwrite other contributors' work without explicit coordination.
- Do not make large unrelated refactors while implementing an issue.
- Before modifying shared entry points such as `MauiProgram.cs`, `App.xaml`, `MainPage.xaml`, or `MainPage.xaml.cs`, check whether another contributor owns them.
- If the working tree is not clean, inspect the changes and preserve them. Do not discard work to make tests pass.
- If a requirement is ambiguous or conflicts with the roadmap, explain the conflict and ask/record a decision rather than silently choosing a risky behavior.
- If a phase's exit gate is not met, do not mark downstream work complete. You may do independent preparatory work, but report the dependency.

## Definition of done

An issue is done only when its acceptance criteria are implemented, relevant tests are added/run, the Android build result is recorded, documentation is updated, security and approval requirements are preserved, and the GitHub issue reflects the verified status.

**Important:** `PROJECT-ROADMAP.md` is the canonical project order. Do not treat the issue-number order alone as the implementation order.
