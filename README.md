# QuanLyCuDan

ASP.NET Core MVC application for resident management, using Razor Views, EF Core and SQL Server. Controllers process ordinary HTML forms and return `View(model)` or redirect after successful writes. There are no JSON API controllers.

## Run locally

1. Install the .NET 8 and .NET 9 SDKs. The main application targets .NET 8; the separate SignalRChat sample targets .NET 9.
2. Create the COMPACT-2.0 database using `QuanLyCuDan.sql`. The script safely refuses to overwrite an incompatible existing database. Rerun this same script on an existing COMPACT-2.0 database to apply the registration-profile and announcement-review upgrades while preserving data and passwords. For SQL Express, run `sqlcmd -S .\SQLEXPRESS -E -C -b -f 65001 -i QuanLyCuDan.sql`. The `Doc` directory is intentionally excluded from this repository.
3. Set `ConnectionStrings:MyStock` in a local `QuanLyCuDan/appsettings.Development.json`, or use the `ConnectionStrings__MyStock` environment variable. Local development settings are ignored by Git. The committed default uses Windows Authentication on localhost.
4. Run `dotnet run --project QuanLyCuDan --launch-profile http` and open http://localhost:5044/ for the public portal, or http://localhost:5044/Resident for resident services.

For SQL Express, the local development connection string is `Server=.\SQLEXPRESS;Database=QuanLyCuDan;Integrated Security=True;Encrypt=False;TrustServerCertificate=True`. Set it in the ignored development settings file mentioned above. The supplied SQL script contains fictional demo accounts `dan01`, `dan02`, `dan03` with password `12345678`. Approved residents enter `/Resident` after login; system account managers enter `/Admin`.

For a database installed before the resident signup form was added, run `sqlcmd -S .\SQLEXPRESS -E -C -I -b -f 65001 -i database/20261109_RegistrationProfile.sql` once before starting the updated app. This idempotent upgrade adds one nullable column for pending registration details; existing accounts and resident records are preserved. Fresh databases created by the current `QuanLyCuDan.sql` already have the column.

For an existing database, also run `sqlcmd -S .\SQLEXPRESS -E -C -I -b -f 65001 -i database/20261109_AnnouncementReview.sql` before starting the announcement management feature. It adds review fields and `announcements.approve` (GROUP/AREA), assigns the new permission to the seeded OFFICER role, and preserves previously published content. Both upgrade scripts are safe to rerun.

## Public and resident portal

The public homepage supports announcement search, group filtering, pagination and detail pages. Only PUBLIC, published, currently effective announcements belonging to active groups and areas are visible anonymously.

`/Resident` includes personal details and contact updates, current household members, residence requests, private feedback and replies, household fees/receipts, group announcements and personal notifications. All mutations use antiforgery tokens; ownership and current SELF permissions are checked on the server. Contact updates and request cancellation use row versions. Create and reply operations reject reuse of an idempotency key with a different payload. A resident can have one pending/processing residence request at a time.

Residence requests apply to the signed-in resident. Moving into or registering in a household requires an existing receiving household code in the receiving group; this does not create a new person's record. Only the current household head can propose another current member as head. Requests wait for independent staff processing; submitting a form does not change official resident records.

Only the current head can see assessed household charges and receipts. Residents contact the fee collector to pay; the portal does not mark payments as paid. Required and opted-in voluntary assessments use their saved amounts.

For an APPROVED account missing ResidentId, open Registrations > Approved > Details. The screen suggests an unlinked resident with the same citizen ID (CCCD). Verify the displayed identity and confirm the link; ACCOUNT/RESIDENT roles with SELF scope are granted in the same transaction.

New applicants register at `/Account/Register` with their date of birth, gender, optional citizen ID/occupation, address, group and residence type. These details are stored as a typed JSON profile on the pending account; no official resident/household or roles are created at signup. An existing approved account without a linked resident can choose **Bổ sung thông tin cư dân** from `/Account/Profile` and submit the same details at `/Account/ResidentRegistration`. Submission returns that account to PENDING and signs it out until review.

Administrators open Registrations > Pending > Details, compare the submitted information with the existing resident matched by citizen ID (CCCD), confirm the identity, then approve or reject. Approval links that resident, grants ACCOUNT/RESIDENT with SELF scope and records an audit in one transaction. There is no manual resident selector or resident/household creation during review. If CCCD is missing or no unlinked resident matches, approval is blocked: the citizen ID must be supplied and staff must arrange an official resident record first. A standalone staff resident-entry screen is not implemented yet. Reused resident identities, unconfirmed verification and stale or repeated approvals are rejected by the server.

Each pending request can receive one optional PDF/JPEG/PNG attachment up to 5 MB. Files are stored under ignored `QuanLyCuDan/.local/attachments`, outside `wwwroot`, with content signature checks, hashes and authorized downloads. Back up this folder together with the database. Seeded attachment metadata does not create physical files.

## Announcement management

Open `/Announcements` or **Quản lý thông báo** in the staff menu. `/Management/announcements` redirects here. Seeded officers such as `canbo01` enter this screen after login. Access is always limited to active GROUP/AREA grants; a SYSTEM admin role alone does not grant access to resident business data.

Workflow: **Soạn nháp → Gửi duyệt → Duyệt hoặc từ chối → Đăng → Gỡ**. The publication state remains DRAFT/PUBLISHED/UNPUBLISHED; review state is tracked separately. Editing an unpublished draft clears its approval, while pending or published content must first be withdrawn or unpublished. Rejection and removal require reasons. Republishing unchanged approved content is allowed; expired content must have a new future expiration and pass review again after editing. Dates entered in the editor are Vietnam time, stored as UTC.

Permissions are `announcements.read`, `.create`, `.update` (including submit/withdraw), `.approve` (approve/reject), `.publish`, and `.unpublish`. Grant these to staff roles with GROUP or AREA scope. Permissions from different assignments cannot be combined to authorize another group's operation. Users holding all permissions can complete all stages; independent authors/reviewers can be configured through separate roles.

The list supports text, group and status filters with pagination. Each detail page includes preview, permitted actions and the latest 50 audit events. Writes use antiforgery protection and row versions. Publication, audit and per-recipient notifications are committed together. PUBLIC announcements appear on the homepage; RESIDENTS announcements are limited to the correct group. Only approved, currently published, unexpired content is shown to readers. Removal takes it off both reader portals immediately; expired content is filtered without a scheduled job. Notifications link to a scoped resident detail page.

Do not commit SQL credentials, signing keys or real resident data. For a stable JWT signing key, supply `Auth__SigningKey` outside source control as base64 containing at least 32 random bytes.

## Structure

- `QuanLyCuDan/Views`: Razor Views (`.cshtml`) rendered by controller actions with `View(model)`. Layouts and reusable partials live in `Views/Shared`. These views do not use `@page` or `.cshtml.cs` PageModels.
- `QuanLyCuDan/Controllers`: MVC actions for public pages, accounts, administration, registration review, resident services, feedback and announcements. Account, role, permission and role-assignment management use server-rendered forms.
- `BaseController` and `AuthControllerBase`: shared permission checks, cookie sign-in and token lifecycle.
- `QuanLyCuDan/Auth`: form input contracts, JWT issuer and `JwtCookieHandler`, registered under the `Cookies` scheme for MVC and SignalR.
- `QuanLyCuDan/Models`: database-first EF Core models and view models.
- `QuanLyCuDan/wwwroot`: frontend scripts, styles and static assets.
- `QuanLyCuDan/Hubs`: basic authenticated SignalR connection; `/SignalR` is the connection test page.
- `SignalRChat`: separate sample project; not required to run the main application's Hub.

Login is a POST to `AccountController`, which issues an HS256 JWT in the `qlcd.auth` cookie (`HttpOnly`, `SameSite=Strict`, `Secure` outside local HTTP development). The browser sends it with MVC forms and SignalR requests. Authenticated Razor layouts also provide the JWT to `jwt-storage.js`, which stores `{ accessToken, expiresAt }` under `localStorage["qlcd.accessToken"]`; anonymous pages clear that entry after logout, password change or session expiration. Every authenticated request checks the signature, issuer, audience, expiration and database session; changing localStorage does not bypass those checks. The JWT expires with the session, at most 30 days after login; the cookie is a browser-session cookie. Logout revokes the current session, and password changes revoke all sessions immediately. The former API access/refresh token flow is retired. POST actions validate antiforgery tokens. Configure a stable `Auth__SigningKey` to preserve logins across application restarts.

## Checks

With the app running against the local fictional demo database, set `AUTH_TEST_URL` to its URL and run `node .validation/razor-views-smoke.cjs`. This checks MVC routes, rendered action links and typed form fields, layout scripts/styles, cookie authentication, cache headers, antiforgery, logout and attachment routing. The resident portal suite starts with a linked fixture; registration and identity-linking checks live in `registration-onboarding-smoke.cjs`.

Run the notification management checks against the local fictional demo database:

```powershell
powershell -ExecutionPolicy Bypass -File .validation/test-notification-management.ps1 -SqlServer localhost
```

The runner applies additive upgrades, builds the main app, starts an isolated app on port 5199, and runs MVC JWT authentication and announcement workflow tests. It checks draft/edit/submit/reject/approve/publish/unpublish, permissions, public/private visibility, notification recipients, CSRF and concurrency. Temporary workflow fixtures are cleaned up by the suite, and the runner stops its app and restores environment variables. Logs are saved under `QuanLyCuDan/.local/notification-tests`. Use `-Port` to choose another port or `-SqlServer '.\SQLEXPRESS'` for SQL Express. Requires .NET 8+, Node.js 22+ and `sqlcmd` with Windows Authentication; use demo data only.

```powershell
dotnet build QuanLyCuDan.sln
```

For HTTP smoke tests, start the main application with a local demo database, then run (set the SQL server to match the app):

```powershell
$env:AUTH_TEST_URL = 'http://127.0.0.1:5044'
$env:PORTAL_TEST_SQL_SERVER = 'localhost'
node .validation/auth-flow-smoke.cjs
node .validation/controllers-smoke.cjs
```

To test the resident portal against a running app connected to the local fictional demo database:

```powershell
$env:PORTAL_TEST_SQL_SERVER = '.\SQLEXPRESS'
node .validation/resident-portal-smoke.cjs
node .validation/registration-onboarding-smoke.cjs
node .validation/announcements-smoke.cjs
```

This test creates its own verified resident, household, assessments, announcements and notifications, then checks creation/cancellation, contact concurrency, private replies/files, publication filters, CSRF, ownership, fee visibility and permission denials. It removes its fixture records and attachment in `finally`. It requires Windows Authentication and `sqlcmd`; the SQL server must match the running app. Do not run it against production data.

Optional browser QA: `node .validation/portal-browser.cjs` uses an installed Chrome (`CHROME_PATH` overrides its path), checks desktop/mobile overflow and conditional request fields, and saves screenshots under ignored `QuanLyCuDan/.local`. It signs in with fictional `dan03` by default; override `PORTAL_TEST_USER` and `PORTAL_TEST_PASSWORD` if necessary.

For announcement browser QA, set `$env:PORTAL_BROWSER_MODE = 'announcements'` before running that script. It checks staff navigation, list/detail/editor layouts, and safe live preview without publishing content. The announcement integration test uses isolated temporary writer/reviewer accounts and roles, checks GROUP/AREA isolation, review/publish/removal, edit invalidation, expiry, notification recipients and concurrency, and removes its fixtures afterwards. Run the HTTP smoke suites separately or restart the test app between suites if the shared login rate limit is reached.

Override `AUTH_TEST_URL`, `AUTH_TEST_USER` and `AUTH_TEST_PASSWORD` through environment variables when needed. The controller test creates and removes a temporary registration in the local demo database. Only use these tests with demo data.
