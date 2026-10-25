# JobFlow Data Repository Contract

JobFlow Mobile reads from the existing private repository `tmassey1979/Jobflow-data`.

## Read paths

- `daily/`: enumerate dated JSON feed files and select the newest filename in `YYYY-MM-DD.json` form.
- `daily/YYYY-MM-DD.json`: read the feed envelope and `jobs[]`.
- Each feed item includes a stable `job_id`, title/company, compensation, remote scope, verification timestamp, application URL, and relative paths to metadata, resume, cover letter, and tracking JSON.
- `jobs/<job-id>/metadata.json`, `resume.md`, `cover-letter.md`: job details and application-kit drafts.
- `applications/<job-id>.json` and `history/events.jsonl`: application progress/history. Mobile writes are intentionally not implemented yet.

## Authentication

Use a fine-grained GitHub token limited to this repository with Contents: Read-only. Keep it in Android Secure Storage; never hardcode it or log it. GitHub API requests use HTTPS and a User-Agent.

## Safety and consistency

- Treat `job_id` as the deduplication key.
- Preserve the data repo's schemas and existing paths; do not duplicate the data repository into this app repository.
- This first implementation does not push commits, edit tracking JSON, submit applications, or contact employers.
- Offline SQLite persistence, sync timestamps, conflict handling, and approved write/backup operations are follow-up work.
