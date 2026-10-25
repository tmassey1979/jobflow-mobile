# JobFlow Data Repository Contract

JobFlow Mobile reads from the existing private repository `tmassey1979/Jobflow-data`.

## Read paths

- `daily/`: enumerate dated JSON feed files and select the newest filename that exactly matches `YYYY-MM-DD.json`.
- `daily/YYYY-MM-DD.json`: read the feed envelope. Version 1 requires `schema_version: 1` and a `jobs[]` array; `generated_at`, `criteria`, and `verification_summary` may provide feed metadata.
- Each job must have a stable non-empty `job_id`, `title`, and `company`. Compensation, remote scope, verification timestamp, application URL, match score, and relative paths to metadata, resume, cover letter, and tracking JSON are optional fields and must not be fabricated when absent.
- `jobs/<job-id>/metadata.json`, `resume.md`, `cover-letter.md`: job details and application-kit drafts.
- `applications/<job-id>.json` and `history/events.jsonl`: application progress/history. Mobile writes are intentionally not implemented yet.

## Authentication

Use a fine-grained GitHub token limited to this repository with Contents: Read-only. Keep it in Android Secure Storage; never hardcode it or log it. GitHub API requests use HTTPS and a User-Agent. Do not show raw API error response bodies to users.

## Parsing and validation

- Support only explicitly recognized feed schema versions; fail clearly on an unknown version rather than silently misreading it.
- Ignore non-date files in `daily/`; choose the newest valid `YYYY-MM-DD.json` filename.
- Reject malformed envelopes and jobs missing required identity fields.
- Deduplicate jobs by `job_id`, case-insensitively, preserving the first occurrence in the feed.
- Keep parsing logic independent of HTTP so it can be tested with sanitized fixtures.

## Safety and consistency

- Treat `job_id` as the deduplication key.
- Preserve the data repo's schemas and existing paths; do not duplicate the data repository into this app repository.
- This first implementation does not push commits, edit tracking JSON, submit applications, or contact employers.
- Offline SQLite persistence, sync timestamps, conflict handling, and approved write/backup operations are follow-up work.
