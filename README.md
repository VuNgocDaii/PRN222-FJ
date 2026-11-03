# QuanLyCuDan

ASP.NET Core MVC application for resident management. Business logic lives in controllers using EF Core and SQL Server; there is no service layer.

## Run locally

1. Install the .NET 8 and .NET 9 SDKs. The main application targets .NET 8; the separate SignalRChat sample targets .NET 9.
2. Create the COMPACT-2.0 database with the SQL script supplied separately by the project owner. The `Doc` directory is intentionally excluded from this repository.
3. Set `ConnectionStrings:MyStock` in a local `QuanLyCuDan/appsettings.Development.json`, or use the `ConnectionStrings__MyStock` environment variable. Local development settings are ignored by Git. The committed default uses Windows Authentication on localhost.
4. Run `dotnet run --project QuanLyCuDan --launch-profile http` and open http://localhost:5044/Account/Login.

Do not commit SQL credentials, signing keys or real resident data. For a stable JWT signing key, supply `Auth__SigningKey` outside source control as base64 containing at least 32 random bytes.

## Structure

- `QuanLyCuDan/Controllers`: MVC/API actions, database operations, transactions and permission checks.
- `BaseController` and `AuthControllerBase`: common controller checks and authentication code.
- `QuanLyCuDan/Auth`: request contracts, JWT issuer and Bearer authentication handler.
- `QuanLyCuDan/Models`: database-first EF Core models and view models.
- `QuanLyCuDan/Views`, `wwwroot`: Razor views and frontend assets.
- `QuanLyCuDan/Hubs`: basic authenticated SignalR connection; `/SignalR` is the connection test page.
- `SignalRChat`: separate sample project; not required to run the main application's Hub.

API login returns an access token and sets an HttpOnly refresh cookie. The frontend refreshes expired tokens; MVC pages use authentication cookies. There is no `/api/auth/token` endpoint.

## Checks

```powershell
dotnet build QuanLyCuDan.sln
node --test .validation/auth-client.test.cjs
```

For HTTP smoke tests, start the main application on `http://127.0.0.1:5098` with a local demo database, then run:

```powershell
node .validation/auth-flow-smoke.cjs
node .validation/controllers-smoke.cjs
```

Override `AUTH_TEST_URL`, `AUTH_TEST_USER` and `AUTH_TEST_PASSWORD` through environment variables when needed. The controller test creates and removes a temporary registration in the local demo database. Only use these tests with demo data.
