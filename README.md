# QuanLyCuDan

ASP.NET Core MVC application for resident management, using Razor Views, EF Core and SQL Server. Controllers process ordinary HTML forms and return `View(model)` or redirect after successful writes. There are no JSON API controllers.

Script cài đặt và migrate DB đầy đủ: [database/QuanLyCuDan.sql](database/QuanLyCuDan.sql). Danh sách tài khoản test và mật khẩu: [TAI_KHOAN_TEST.md](TAI_KHOAN_TEST.md).

## Run locally

1. Install the .NET 8 and .NET 9 SDKs. The main application targets .NET 8; the separate SignalRChat sample targets .NET 9.
2. Run the complete `database/QuanLyCuDan.sql` installer, which includes the base schema, village migrations and fictional village demo data. Use `sqlcmd -S .\SQLEXPRESS -E -C -b -f 65001 -i database/QuanLyCuDan.sql`. Business data is preserved; the data refresh resets the 17 known seed account passwords to 12345678 once. Other account passwords are preserved. Conflicting business role assignments require explicit resolution. See [village database setup](database/README.md) for migration-only steps and demo logins.
3. Set `ConnectionStrings:MyStock` in a local `QuanLyCuDan/appsettings.Development.json`, or use the `ConnectionStrings__MyStock` environment variable. Local development settings are ignored by Git. The committed default uses Windows Authentication on localhost.
4. Run `dotnet run --project QuanLyCuDan --launch-profile http` and open http://localhost:5044/ for the public portal, or http://localhost:5044/Resident for resident services.

For SQL Express, the local development connection string is `Server=.\SQLEXPRESS;Database=QuanLyCuDan;Integrated Security=True;Encrypt=False;TrustServerCertificate=True`. Set it in the ignored development settings file mentioned above. The supplied SQL script contains fictional demo accounts `dan01`, `dan02`, `dan03` with password `12345678`. Approved residents enter `/Resident` after login; system account managers enter `/Admin`.

The updated application requires VILLAGE-3.0 and VILLAGE-3.1. Use the complete installer or the documented migration sequence; the old base script alone does not install the new authorization view. New village demo accounts use `12345678`; the data refresh also normalizes the known legacy sample account passwords once.

## Public and resident portal

The public homepage supports announcement search, group filtering, pagination and detail pages. Only PUBLIC, published, currently effective announcements belonging to active groups and areas are visible anonymously.

`/Resident` includes personal details and contact updates, current household members, residence requests, private feedback and replies, household fees/receipts, group announcements and personal notifications. All mutations use antiforgery tokens; ownership and current SELF permissions are checked on the server. Contact updates and request cancellation use row versions. Create and reply operations reject reuse of an idempotency key with a different payload. A resident can have one pending/processing residence request at a time.

Residence requests apply to the signed-in resident. Moving into or registering in a household requires an existing receiving household code in the receiving group; this does not create a new person's record. Only the current household head can propose another current member as head. Requests wait for independent staff processing; submitting a form does not change official resident records.

Only the current head can see assessed household charges and receipts. Residents contact the fee collector to pay; the portal does not mark payments as paid. Required and opted-in voluntary assessments use their saved amounts.

For an APPROVED account missing ResidentId, open Registrations > Approved > Details. The screen suggests an unlinked resident with the same citizen ID (CCCD). Verify the displayed identity and confirm the link; the ACCOUNT role is replaced by RESIDENT in the same transaction, preserving assignment history and one current role.

New applicants register at `/Account/Register` with their date of birth, gender, optional citizen ID/occupation, address, group and residence type. These details are stored as a typed JSON profile on the pending account; no official resident/household or roles are created at signup. An existing approved account without a linked resident can choose **Bổ sung thông tin cư dân** from `/Account/Profile` and submit the same details at `/Account/ResidentRegistration`. Submission returns that account to PENDING and signs it out until review.

Administrators open Registrations > Pending > Details and approve or reject. If a matching unlinked resident exists, approval links it and assigns RESIDENT. Otherwise it assigns ACCOUNT without resident access; a verified match can be linked later. Both paths maintain one role and audit history. Reused identities, stale and repeated approvals are rejected. No resident/household record is created by registration review.

Each pending request can receive one optional PDF/JPEG/PNG attachment up to 5 MB. Files are stored under ignored `QuanLyCuDan/.local/attachments`, outside `wwwroot`, with content signature checks, hashes and authorized downloads. Back up this folder together with the database. Seeded attachment metadata does not create physical files.

## Announcement management

Open `/Announcements` or **Quản lý thông báo** in the staff menu. `/Management/announcements` redirects here. Seeded officers such as `canbo01` enter this screen after login. Access is always limited to active GROUP/AREA grants; a SYSTEM admin role alone does not grant access to resident business data.

Workflow: **Soạn nháp → Gửi duyệt → Chấp thuận và tự động đăng / Từ chối → Gỡ**. Only VILLAGE_HEAD and PARTY_SECRETARY can approve or remove content within their village. Either is sufficient; self approval is allowed. Approval, publication, audit and recipient notifications commit in one transaction. There is no manual publish step. Editing content or replacing its PDF clears prior approval.

Each account has one current role. Personal SELF permissions and the role's business GROUP/AREA/SYSTEM boundary coexist in the effective permission view. Optional OrganizationId narrows a GROUP grant to one organization. Organization heads/deputies cannot approve or remove posts. Staff edit/submit their own drafts; village leaders can manage drafts across their village.

PUBLIC posts appear anonymously; RESIDENTS posts stay within the village; ORGANIZATION posts require current membership and a linked resident within that village. Active date, publication and moderation checks apply to details and document downloads as well as lists. Organization membership is managed at /Organizations. Directive drafts have a deadline and private PDF document; they use the same approval workflow. See [implemented village rules and demo accounts](database/README.md).

Do not commit SQL credentials, signing keys or real resident data. For a stable JWT signing key, supply `Auth__SigningKey` outside source control as base64 containing at least 32 random bytes.

## Structure

`/Admin` preserves the original account table, role-assignment panels and sidebar tabs for accounts, roles and permissions. Tab and selector changes are ordinary MVC GET requests; writes post to the existing controllers with antiforgery and a validated local return URL so success and validation messages stay on the selected admin tab. No API requests are used. Run `node .validation/admin-browser.cjs` against the local demo app to check the restored desktop/mobile UI and dialogs.

- `QuanLyCuDan/Views`: Razor Views (`.cshtml`) rendered by controller actions with `View(model)`. Layouts and reusable partials live in `Views/Shared`. These views do not use `@page` or `.cshtml.cs` PageModels.
- `QuanLyCuDan/Controllers`: MVC actions for public pages, accounts, administration, registration review, resident services, feedback and announcements. Account, role, permission and role-assignment management use server-rendered forms.
- `BaseController` and `AuthControllerBase`: shared permission checks, cookie sign-in and token lifecycle.
- `QuanLyCuDan/Auth`: form input contracts, JWT issuer and `JwtCookieHandler`, registered under the `Cookies` scheme for MVC and SignalR.
- `QuanLyCuDan/Models`: database-first EF Core models and view models; village extensions use partial classes.
- `QuanLyCuDan/Services`: business rules, permission/scope checks, transactions and EF Core queries.
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

The runner installs and reruns the complete schema on an isolated `QuanLyCuDan_VillageValidation` database, builds the app, and runs village workflow, onboarding, MVC administration and resident regression suites. It restarts the app between suites to respect login rate limits, removes temporary fixtures, and saves logs under `.local/village-tests`. Use `-Database` (must contain VillageValidation), `-Port`, or `-SqlServer` to override defaults. The application database is not migrated by this runner.

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
$env:PORTAL_TEST_DATABASE = 'QuanLyCuDan_VillageValidation' # must match the app connection
node .validation/resident-portal-smoke.cjs
node .validation/registration-onboarding-smoke.cjs
node .validation/village-workflow-smoke.cjs
```

This test creates its own verified resident, household, assessments, announcements and notifications, then checks creation/cancellation, contact concurrency, private replies/files, publication filters, CSRF, ownership, fee visibility and permission denials. It removes its fixture records and attachment in `finally`. It requires Windows Authentication and `sqlcmd`; the SQL server must match the running app. Do not run it against production data.

Optional browser QA: `node .validation/portal-browser.cjs` uses an installed Chrome (`CHROME_PATH` overrides its path), checks desktop/mobile overflow and conditional request fields, and saves screenshots under ignored `QuanLyCuDan/.local`. It signs in with fictional `dan03` by default; override `PORTAL_TEST_USER` and `PORTAL_TEST_PASSWORD` if necessary.

For announcement browser QA, set `$env:PORTAL_BROWSER_MODE = 'announcements'` before running that script. It checks staff navigation, list/detail/editor layouts, and safe live preview without publishing content. The village integration test uses fictional village accounts and removes its temporary content. It checks single-role enforcement, organization isolation, leader-only approval/removal, auto publication, re-review after edits, scoped recipients and documents. Run the HTTP smoke suites separately or restart the test app between suites if the shared login rate limit is reached.

Override `AUTH_TEST_URL`, `AUTH_TEST_USER` and `AUTH_TEST_PASSWORD` through environment variables when needed. The controller test creates and removes a temporary registration in the local demo database. Only use these tests with demo data.
