# Local verification

Performed on 17 October 2026 (Europe/Minsk).

- .NET SDK 10.0.402 installed locally in the ignored .tools directory.
- ASP.NET Core / EF packages restored successfully with no NuGet audit warnings at the final restore.
- .NET solution compiled with 0 errors and 0 warnings.
- 32 xUnit rule and API integration tests passed, 0 failed, 0 skipped.
- SQLite InitialCreate migration applied to new databases.
- React production build succeeded with Vite.
- 12 browser checks passed in Microsoft Edge through Playwright, with real API requests and a separate disposable SQLite database.
- Desktop: 1440 × 1050. Mobile: 390 × 844, no horizontal overflow.
- Covered UI paths: board, list, team, report, CSV download, create/edit, assignee validation, start/complete, history, reload persistence, filter reset, empty result, stale-version conflict, keyboard dismissal and network-error retry.
- No JavaScript page errors during the browser run.

Screenshots in this folder's screenshots subdirectory were captured from the running application. Some show requests created by the browser tests; those were created in the disposable test database, not the user's demo database.

Not verified: PostgreSQL / Docker (Docker not available here), GitHub Actions execution on GitHub (workflow is provided but not published), production deployment, authentication, load/SLO targets and full accessibility conformance.

Detailed local machine reports: artifacts/browser-verification.json and artifacts/test-results/*.trx (excluded from source control). Tests can be rerun using the scripts in the README.
