param(
    [string]$SqlServer = 'localhost',
    [ValidateRange(1024, 65535)][int]$Port = 5199,
    [ValidatePattern('^[A-Za-z0-9_]*VillageValidation[A-Za-z0-9_]*$')][string]$Database = 'QuanLyCuDan_VillageValidation'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'QuanLyCuDan'
$artifacts = Join-Path $repo '.local/village-tests'
$baseUrl = "http://127.0.0.1:$Port"
$appProcess = $null
$savedEnvironment = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT','ConnectionStrings__MyStock','AUTH_TEST_URL','PORTAL_TEST_SQL_SERVER','PORTAL_TEST_DATABASE')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
}
Push-Location $repo
try {
    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$Port)
    try { $listener.Start() } finally { $listener.Stop() }
    $sqlFile = Join-Path $artifacts 'install.sql'
    $sql = [IO.File]::ReadAllText((Join-Path $repo 'database/QuanLyCuDan.sql')).Replace('QuanLyCuDan',$Database)
    [IO.File]::WriteAllText($sqlFile,$sql,[Text.UTF8Encoding]::new($true))
    & sqlcmd -S $SqlServer -E -C -b -f 65001 -i $sqlFile -o "$artifacts/schema.log"
    if ($LASTEXITCODE -ne 0) { throw "Schema installation failed. See $artifacts/schema.log" }
    & sqlcmd -S $SqlServer -E -C -b -f 65001 -i $sqlFile -o "$artifacts/schema-rerun.log"
    if ($LASTEXITCODE -ne 0) { throw 'Schema rerun failed.' }
    & dotnet build QuanLyCuDan/QuanLyCuDan.csproj --no-restore -o "$artifacts/app" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__MyStock = "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
    $env:AUTH_TEST_URL = $baseUrl
    $env:PORTAL_TEST_SQL_SERVER = $SqlServer
    $env:PORTAL_TEST_DATABASE = $Database
    $appDll = Join-Path $artifacts 'app/QuanLyCuDan.dll'
    foreach ($suite in @('system-roles-smoke','village-workflow-smoke','registration-onboarding-smoke','controllers-smoke','resident-portal-smoke')) {
        $appProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList "`"$appDll`" --urls $baseUrl" -WorkingDirectory $project -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifacts/$suite.log" -RedirectStandardError "$artifacts/$suite-error.log"
        $ready = $false
        $deadline = (Get-Date).AddSeconds(45)
        while ((Get-Date) -lt $deadline) {
            if ($appProcess.HasExited) { throw "App exited. See $artifacts/$suite-error.log" }
            try {
                if ((Invoke-WebRequest "$baseUrl/Account/Login" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Milliseconds 300 }
        }
        if (!$ready) { throw 'Application did not become ready.' }
        & node ".validation/$suite.cjs"
        if ($LASTEXITCODE -ne 0) { throw "$suite failed." }
        Stop-Process -Id $appProcess.Id
        $appProcess.WaitForExit()
        $appProcess = $null
    }
    Write-Host 'PASS: village schema, rerun, workflow, onboarding, MVC and resident regression suites.'
} finally {
    if ($null -ne $appProcess -and !$appProcess.HasExited) { Stop-Process -Id $appProcess.Id -ErrorAction SilentlyContinue }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process') }
    Pop-Location
}
