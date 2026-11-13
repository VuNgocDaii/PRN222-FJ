param([string]$Server = 'localhost')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runId = (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + ([guid]::NewGuid().ToString('N').Substring(0,8))
$testDatabase = 'QuanLyCuDan_PortalTest_' + $runId
if ($testDatabase -notmatch '^QuanLyCuDan_PortalTest_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$') { throw 'Unsafe test database name' }
$artifacts = Join-Path $repo ('.local/organization-db-test/' + $runId)
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$script:stage = 0
$script:passed = 0

function Invoke-Query([string]$Database, [string]$Query, [switch]$AllowFailure) {
    $script:stage++
    $file = Join-Path $artifacts ('query-' + $script:stage + '.sql')
    [IO.File]::WriteAllText($file, 'SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; ' + $Query, $utf8)
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $result = & sqlcmd -S $Server -d $Database -E -C -I -b -l 10 -h -1 -y 4000 -w 65535 -i $file 2>&1; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previousPreference }
    $result | Set-Content -LiteralPath (Join-Path $artifacts ('query-' + $script:stage + '.log'))
    if ($code -ne 0 -and !$AllowFailure) { throw ('SQL stage ' + $script:stage + ' failed: ' + ($result -join "`n")) }
    return [pscustomobject]@{ Code=$code; Text=($result -join "`n").Trim() }
}
function Invoke-Deployment([string]$FileName, [switch]$ExpectFailure) {
    $script:stage++
    $log = Join-Path $artifacts ('deployment-' + $script:stage + '.log')
    # SQLCMD-mode declarations avoid Windows native argv quoting of a backup path containing spaces.
    $declarations = ":setvar PortalTargetDatabase `"$testDatabase`"`n:setvar PortalGroupCode `"TO-A`"`n:setvar PortalBackupFile `"$backupFile`"`n:setvar PortalAllowCatalogRemoval `"YES`"`n"
    $deploymentFile = Join-Path $artifacts ('deployment-' + $script:stage + '.sql')
    [IO.File]::WriteAllText($deploymentFile, $declarations + [IO.File]::ReadAllText((Join-Path $repo ('database/' + $FileName)), [Text.Encoding]::UTF8), $utf8)
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $result = & sqlcmd -S $Server -d $testDatabase -E -C -I -b -l 10 -f 65001 -i $deploymentFile 2>&1; $code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previousPreference }
    $result | Set-Content -LiteralPath $log
    if ($ExpectFailure) { if ($code -eq 0) { throw 'Expected protected deployment to fail' } }
    elseif ($code -ne 0) { throw ('Deployment failed: ' + ($result -join "`n")) }
}
function Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw ('FAIL: ' + $Message) }
    $script:passed++
    Write-Output ('PASS: ' + $Message)
}
function Snapshot {
    return (Invoke-Query $testDatabase "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT Id,UserName,VerificationStatus,ResidentId FROM dbo.AspNetUsers ORDER BY Id FOR JSON PATH)),2), CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.Residents ORDER BY Id FOR JSON PATH)),2), CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.Households ORDER BY Id FOR JSON PATH)),2), CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.Requests ORDER BY Id FOR JSON PATH)),2);").Text
}

# Reuse the existing fictional baseline ONLY after substituting every database-context guard.
# The original QuanLyCuDan.sql is never executed or edited by this test.
$baseline = [IO.File]::ReadAllText((Join-Path $repo 'QuanLyCuDan.sql'), [Text.Encoding]::UTF8)
$baseline = $baseline.Replace("N'QuanLyCuDan'", "N'$testDatabase'").Replace('[QuanLyCuDan]', "[$testDatabase]")
if ($baseline.Contains('USE [QuanLyCuDan]') -or $baseline.Contains('CREATE DATABASE [QuanLyCuDan]') -or !$baseline.Contains("DB_NAME()<>N'$testDatabase'")) { throw 'Test isolation guard failed before execution' }
$baselineFile = Join-Path $artifacts 'isolated-baseline.sql'
[IO.File]::WriteAllText($baselineFile, $baseline, $utf8)
$result = & sqlcmd -S $Server -d master -E -C -I -b -l 10 -f 65001 -i $baselineFile 2>&1
if ($LASTEXITCODE -ne 0) { throw ($result -join "`n") }
$result | Set-Content (Join-Path $artifacts 'baseline.log')
Check ((Invoke-Query $testDatabase 'SELECT DB_NAME();').Text -eq $testDatabase) 'A separate, uniquely named test database was created'

$backupDirectory = (Invoke-Query master "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultBackupPath'));").Text.Trim()
if (!$backupDirectory -or $backupDirectory -eq 'NULL') { throw 'SQL Server has no known backup directory; stop before migration' }
$backupFile = Join-Path $backupDirectory ($testDatabase + '_before_organizations.bak')
$backupSql = $backupFile.Replace("'", "''")
$null = Invoke-Query master "BACKUP DATABASE [$testDatabase] TO DISK=N'$backupSql' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$backupSql' WITH CHECKSUM;"
$before = Snapshot
Invoke-Deployment '20261113_PortalOrganizations.sql'
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizations;').Text -eq '10') '10 catalog organizations from the Word document'
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizationPositions;').Text -eq '16') '16 documented positions; no invented senior/security leader or dance position'
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizationAssignments; SELECT COUNT(*) FROM dbo.PortalOrganizationMembers;').Text -replace '\s','' -eq '00') 'No people, appointments or memberships were seeded'
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizationPositions WHERE MinimumCount IS NULL AND MaximumCount IS NULL;').Text -eq '7') '7 positions retain unspecified headcounts'
Check ((Invoke-Query $testDatabase "SELECT COUNT(*) FROM dbo.PortalOrganizationPositions p JOIN dbo.PortalOrganizations o ON o.Id=p.OrganizationId WHERE (o.Code='CHI_BO' AND p.Code='BI_THU' AND p.MinimumCount=1 AND p.MaximumCount=1) OR (o.Code='CHI_BO' AND p.Code='PHO_BI_THU' AND p.MinimumCount=2 AND p.MaximumCount=3) OR (o.Code='LANH_DAO_THON' AND p.Code='TRUONG_THON' AND p.MinimumCount=1 AND p.MaximumCount=1) OR (o.Code='LANH_DAO_THON' AND p.Code='PHO_TRUONG_THON' AND p.MinimumCount=2 AND p.MaximumCount=3) OR (o.Code IN('MAT_TRAN','THANH_NIEN') AND p.MinimumCount=2 AND p.MaximumCount=2) OR (o.Code IN('NONG_DAN','PHU_NU','CUU_CHIEN_BINH') AND p.MinimumCount=3 AND p.MaximumCount=3);").Text -eq '9') 'All 9 explicit quotas match the source'
Check ((Invoke-Query $testDatabase "SELECT COUNT(*) FROM dbo.PortalOrganizations o JOIN dbo.ResidentialUnits g ON g.Id=o.ResidentialGroupId WHERE g.Code<>'TO-A';").Text -eq '0') 'Organization foreign keys use the actual TO-A ID only'
Check ((Snapshot) -eq $before) 'Accounts, residents, households and request snapshots are unchanged'
Invoke-Deployment '20261113_PortalOrganizations.sql'
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizations; SELECT COUNT(*) FROM dbo.PortalOrganizationPositions;').Text -replace '\s','' -eq '1016') 'A second seed run creates no duplicates'

$duplicate = Invoke-Query $testDatabase "INSERT dbo.PortalOrganizations(ResidentialGroupId,Code,Name,Kind) SELECT ResidentialGroupId,Code,Name,Kind FROM dbo.PortalOrganizations WHERE Code='CHI_BO';" -AllowFailure
Check ($duplicate.Code -ne 0) 'Unique organization code constraint rejects duplicates'
$invalidQuota = Invoke-Query $testDatabase "UPDATE dbo.PortalOrganizationPositions SET MinimumCount=4,MaximumCount=2 WHERE Code='PHO_BI_THU' AND OrganizationId=(SELECT Id FROM dbo.PortalOrganizations WHERE Code='CHI_BO');" -AllowFailure
Check ($invalidQuota.Code -ne 0) 'Quota range constraint rejects invalid minimum/maximum'
$invalidPerson = Invoke-Query $testDatabase 'INSERT dbo.PortalOrganizationMembers(OrganizationId,ResidentId) SELECT TOP(1) Id,9223372036854775807 FROM dbo.PortalOrganizations;' -AllowFailure
Check ($invalidPerson.Code -ne 0) 'Membership requires an existing resident'
$invalidConsent = Invoke-Query $testDatabase 'INSERT dbo.PortalOrganizationAssignments(PositionId,ResidentId,IsConfirmed,PublishName) SELECT TOP(1) p.Id,r.Id,0,1 FROM dbo.PortalOrganizationPositions p CROSS JOIN dbo.Residents r;' -AllowFailure
Check ($invalidConsent.Code -ne 0) 'An unconfirmed appointment cannot publish a name'
$null = Invoke-Query $testDatabase "INSERT dbo.PortalOrganizationMembers(OrganizationId,ResidentId,IsConfirmed) SELECT TOP(2) o.Id,r.Id,1 FROM dbo.PortalOrganizations o CROSS JOIN dbo.Residents r WHERE r.Id=(SELECT ResidentId FROM dbo.AspNetUsers WHERE UserName='dan01') ORDER BY o.Id;"
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizationMembers;').Text -eq '2') 'One existing resident can join multiple organizations in the isolated test'
Invoke-Deployment '20261113_PortalOrganizations_Rollback.sql' -ExpectFailure
Check ((Invoke-Query $testDatabase 'SELECT COUNT(*) FROM dbo.PortalOrganizationMembers;').Text -eq '2') 'Rollback refuses removal when memberships exist'
$null = Invoke-Query $testDatabase 'DELETE dbo.PortalOrganizationMembers;' # Only the two fixtures inside the guarded disposable test DB.
Invoke-Deployment '20261113_PortalOrganizations_Rollback.sql'
Check ((Invoke-Query $testDatabase "SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'PortalOrganization%';").Text -eq '0') 'Rollback removes only the unused new schema'
Check ((Snapshot) -eq $before) 'Rollback preserves all baseline business and account data'
Invoke-Deployment '20261113_PortalOrganizations.sql'
Check ((Invoke-Query $testDatabase "SELECT Status FROM dbo.Requests WHERE Code='YC-DEMO-001'; SELECT Status FROM dbo.Requests WHERE Code='YC-DEMO-002';").Text -replace '\s','' -eq 'PENDINGPROCESSING') 'The two demo request states are preserved'

$metadata = [pscustomobject]@{ Server=$Server; Database=$testDatabase; BackupFile=$backupFile; Passed=$script:passed; Failed=0; Artifacts=$artifacts; CreatedAt=(Get-Date).ToString('o') }
$metadata | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $repo '.local/organization-db-test/latest.json')
$metadata | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $artifacts 'result.json')
Write-Output ('RESULT: ' + $script:passed + ' passed, 0 failed. Shared QuanLyCuDan database was not modified.')
Write-Output ('TEST_DATABASE: ' + $testDatabase)
Write-Output ('BACKUP: ' + $backupFile)
Write-Output ('ARTIFACTS: ' + $artifacts)
