/* Conservative rollback of ONLY the new, unused portal catalog schema.
   SSMS SQLCMD Mode / sqlcmd variables: PortalTargetDatabase, PortalGroupCode, PortalBackupFile, PortalAllowCatalogRemoval="YES".
   Requires a verified backup and refuses actual assignments, memberships, other-locality catalogs or unknown catalog sizes.
   If any real data has been entered, DO NOT delete it: review a restore of the pre-deployment backup in an isolated database.
   Do not restore over a shared live database without reconciling all changes since the backup and obtaining approval.
*/
USE [$(PortalTargetDatabase)];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(PortalAllowCatalogRemoval)'<>N'YES' THROW 51120,'Explicit catalog rollback confirmation is required.',1;
DECLARE @BackupFile nvarchar(260)=N'$(PortalBackupFile)';
IF NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily f ON f.media_set_id=b.media_set_id
    WHERE b.database_name=DB_NAME() AND b.type='D' AND b.backup_finish_date IS NOT NULL AND f.physical_device_name=@BackupFile)
    THROW 51121,'A completed backup of this target is required.',1;
RESTORE VERIFYONLY FROM DISK=@BackupFile WITH CHECKSUM;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='PORTAL-ORG-1.0')
BEGIN PRINT 'Organization feature is not installed. No changes.'; RETURN; END;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'PortalOrganizations.1.0',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51122,'Could not obtain organization deployment lock.',1;
    IF EXISTS(SELECT 1 FROM dbo.PortalOrganizationAssignments) OR EXISTS(SELECT 1 FROM dbo.PortalOrganizationMembers)
        THROW 51123,'Actual assignments or memberships exist. Rollback refused to protect data.',1;
    IF EXISTS(SELECT 1 FROM dbo.PortalOrganizations o JOIN dbo.ResidentialUnits g ON g.Id=o.ResidentialGroupId WHERE g.Code<>'$(PortalGroupCode)')
        THROW 51124,'Another locality has organization data. Rollback refused.',1;
    IF (SELECT COUNT(*) FROM dbo.PortalOrganizations)<>10 OR (SELECT COUNT(*) FROM dbo.PortalOrganizationPositions)<>16
        THROW 51125,'Catalog was extended or modified. Review instead of removing it.',1;
    DROP TABLE dbo.PortalOrganizationMembers;
    DROP TABLE dbo.PortalOrganizationAssignments;
    DROP TABLE dbo.PortalOrganizationPositions;
    DROP TABLE dbo.PortalOrganizations;
    DELETE dbo.SchemaVersions WHERE VersionCode='PORTAL-ORG-1.0';
    COMMIT;
    PRINT 'Unused organization catalog removed. Existing business/authentication data was not changed.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
