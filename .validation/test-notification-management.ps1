param(
    [string]$SqlServer = 'localhost',
    [ValidateRange(1024, 65535)][int]$Port = 5199
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'QuanLyCuDan'
$artifacts = Join-Path $project '.local/notification-tests'
$baseUrl = "http://127.0.0.1:$Port"
$appProcess = $null
$savedEnvironment = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'ConnectionStrings__MyStock', 'AUTH_TEST_URL', 'PORTAL_TEST_SQL_SERVER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location $repo
try {
    foreach ($command in @('dotnet', 'node', 'sqlcmd')) {
        Get-Command $command -ErrorAction Stop | Out-Null
    }
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
    try { $listener.Start() } finally { $listener.Stop() }

    # Requires the local fictional demo database. Never recreates or reseeds it.
    & sqlcmd -S $SqlServer -E -C -I -b -f 65001 -i database/20261109_RegistrationProfile.sql
    if ($LASTEXITCODE -ne 0) { throw 'Registration database upgrade failed.' }
    & sqlcmd -S $SqlServer -E -C -I -b -f 65001 -i database/20261109_AnnouncementReview.sql
    if ($LASTEXITCODE -ne 0) { throw 'Announcement database upgrade failed.' }

    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    & dotnet build QuanLyCuDan/QuanLyCuDan.csproj -o "$artifacts/app" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__MyStock = "Server=$SqlServer;Database=QuanLyCuDan;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
    $env:AUTH_TEST_URL = $baseUrl
    $env:PORTAL_TEST_SQL_SERVER = $SqlServer
    $appDll = Join-Path $artifacts 'app/QuanLyCuDan.dll'
    $appProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList "`"$appDll`" --urls $baseUrl" -WorkingDirectory $project -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifacts/server.log" -RedirectStandardError "$artifacts/server-error.log"
    $ready = $false
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        if ($appProcess.HasExited) { throw "Application exited. See $artifacts/server-error.log" }
        try {
            $response = Invoke-WebRequest "$baseUrl/Account/Login" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (!$ready) { throw "Application did not become ready. See $artifacts/server.log" }

    & node .validation/auth-flow-smoke.cjs
    if ($LASTEXITCODE -ne 0) { throw 'MVC JWT authentication tests failed.' }
    & node .validation/announcements-smoke.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Notification management tests failed.' }
    Write-Host 'PASS: notification management build and workflow tests.'
} finally {
    if ($null -ne $appProcess -and !$appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -ErrorAction SilentlyContinue
    }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    Pop-Location
}
