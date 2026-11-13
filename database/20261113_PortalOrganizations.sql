/*
  PORTAL ORGANIZATIONS 1.0 — additive, database-first SQL Server deployment.
  Source: 1 bí thư.docx and the owner's clarified list. Catalog/quotas only; NO people/members are seeded.
  Do not run against the shared database without review, a fresh backup and approval.

  SSMS: Query > SQLCMD Mode. Set the three variables below (uncomment and edit), then run the WHOLE script.
  -- :setvar PortalTargetDatabase "YOUR_REVIEWED_DATABASE"
  -- :setvar PortalGroupCode "TO-A"
  -- :setvar PortalBackupFile "FULL_PATH_TO_A_VERIFIED_BACKUP_OF_THAT_DATABASE.bak"
  CLI: sqlcmd -S localhost -E -C -I -b -f 65001 -i database/20261113_PortalOrganizations.sql
       -v PortalTargetDatabase="..." PortalGroupCode="TO-A" PortalBackupFile="..."

  Before deployment: BACKUP DATABASE [reviewed target] TO DISK=N'new unique full path.bak' WITH COPY_ONLY, CHECKSUM;
  This script verifies that backup belongs to the target. It never creates a database or edits existing business records.
  Rerun: only missing catalog codes are inserted. Existing catalog entries are never overwritten.
  No EF migrations are used in this database-first project; do not also call EnsureCreated/Migrate on production.
*/
USE [$(PortalTargetDatabase)];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'$(PortalTargetDatabase)' THROW 51100, 'Unexpected database. Nothing was changed.', 1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='COMPACT-2.0')
    THROW 51101, 'COMPACT-2.0 is required. Nothing was changed.', 1;
DECLARE @BackupFile nvarchar(260)=N'$(PortalBackupFile)';
IF NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily f ON f.media_set_id=b.media_set_id
    WHERE b.database_name=DB_NAME() AND b.type='D' AND b.backup_finish_date IS NOT NULL AND f.physical_device_name=@BackupFile)
    THROW 51102, 'A completed full backup of this exact target is required.', 1;
RESTORE VERIFYONLY FROM DISK=@BackupFile WITH CHECKSUM;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'PortalOrganizations.1.0',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51103, 'Could not obtain organization deployment lock.', 1;
    DECLARE @GroupId bigint;
    SELECT @GroupId=g.Id FROM dbo.ResidentialUnits g JOIN dbo.ResidentialUnits a ON a.Id=g.ParentUnitId
    WHERE g.Code='$(PortalGroupCode)' AND g.UnitType='GROUP' AND g.IsActive=1 AND a.UnitType='AREA' AND a.IsActive=1;
    IF @GroupId IS NULL THROW 51104, 'Configured GROUP and its AREA must exist and be active.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='PORTAL-ORG-1.0') AND
       (OBJECT_ID('dbo.PortalOrganizations','U') IS NOT NULL OR OBJECT_ID('dbo.PortalOrganizationPositions','U') IS NOT NULL OR
        OBJECT_ID('dbo.PortalOrganizationAssignments','U') IS NOT NULL OR OBJECT_ID('dbo.PortalOrganizationMembers','U') IS NOT NULL)
        THROW 51105, 'Partial/unrecognized organization schema found. Review it instead of overwriting.', 1;

    IF OBJECT_ID('dbo.PortalOrganizations','U') IS NULL
    BEGIN
        CREATE TABLE dbo.PortalOrganizations (
            Id bigint IDENTITY NOT NULL CONSTRAINT PK_PortalOrganizations PRIMARY KEY,
            ResidentialGroupId bigint NOT NULL,
            Code varchar(40) NOT NULL, Name nvarchar(200) NOT NULL,
            Kind varchar(24) NOT NULL, Description nvarchar(2000) NULL, SourceNote nvarchar(1000) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_PortalOrg_Active DEFAULT 1,
            IsPublic bit NOT NULL CONSTRAINT DF_PortalOrg_Public DEFAULT 1,
            CONSTRAINT FK_PortalOrg_Group FOREIGN KEY(ResidentialGroupId) REFERENCES dbo.ResidentialUnits(Id),
            CONSTRAINT UQ_PortalOrg_GroupCode UNIQUE(ResidentialGroupId,Code),
            CONSTRAINT CK_PortalOrg_Kind CHECK(Kind IN('PARTY','LOCAL_GOVERNMENT','FRONT','ASSOCIATION','SECURITY','SOCIAL')),
            CONSTRAINT CK_PortalOrg_Name CHECK(LEN(LTRIM(RTRIM(Name)))>0 AND LEN(LTRIM(RTRIM(Code)))>0)
        );
        CREATE INDEX IX_PortalOrg_Visibility ON dbo.PortalOrganizations(ResidentialGroupId,IsActive,IsPublic);
    END;
    IF OBJECT_ID('dbo.PortalOrganizationPositions','U') IS NULL
    BEGIN
        CREATE TABLE dbo.PortalOrganizationPositions (
            Id bigint IDENTITY NOT NULL CONSTRAINT PK_PortalOrganizationPositions PRIMARY KEY,
            OrganizationId bigint NOT NULL, Code varchar(40) NOT NULL, Name nvarchar(200) NOT NULL,
            MinimumCount int NULL, MaximumCount int NULL,
            NeedsClarification bit NOT NULL CONSTRAINT DF_PortalPosition_Clarification DEFAULT 0,
            SourceText nvarchar(500) NULL, SourceNote nvarchar(1000) NULL,
            CONSTRAINT FK_PortalPosition_Org FOREIGN KEY(OrganizationId) REFERENCES dbo.PortalOrganizations(Id),
            CONSTRAINT UQ_PortalPosition_Code UNIQUE(OrganizationId,Code),
            CONSTRAINT CK_PortalPosition_Name CHECK(LEN(LTRIM(RTRIM(Name)))>0 AND LEN(LTRIM(RTRIM(Code)))>0),
            CONSTRAINT CK_PortalPosition_Count CHECK((MinimumCount IS NULL OR MinimumCount>0) AND
                (MaximumCount IS NULL OR MaximumCount>0) AND (MinimumCount IS NULL OR MaximumCount IS NULL OR MinimumCount<=MaximumCount))
        );
    END;
    IF OBJECT_ID('dbo.PortalOrganizationAssignments','U') IS NULL
    BEGIN
        CREATE TABLE dbo.PortalOrganizationAssignments (
            Id bigint IDENTITY NOT NULL CONSTRAINT PK_PortalOrganizationAssignments PRIMARY KEY,
            PositionId bigint NOT NULL, ResidentId bigint NOT NULL, StartsOn date NULL, EndsOn date NULL,
            IsActive bit NOT NULL CONSTRAINT DF_PortalAssignment_Active DEFAULT 1,
            IsConfirmed bit NOT NULL CONSTRAINT DF_PortalAssignment_Confirmed DEFAULT 0,
            PublishName bit NOT NULL CONSTRAINT DF_PortalAssignment_Public DEFAULT 0,
            CONSTRAINT FK_PortalAssignment_Position FOREIGN KEY(PositionId) REFERENCES dbo.PortalOrganizationPositions(Id),
            CONSTRAINT FK_PortalAssignment_Resident FOREIGN KEY(ResidentId) REFERENCES dbo.Residents(Id),
            CONSTRAINT UQ_PortalAssignment_Person UNIQUE(PositionId,ResidentId),
            CONSTRAINT CK_PortalAssignment_Dates CHECK(StartsOn IS NULL OR EndsOn IS NULL OR EndsOn>=StartsOn),
            CONSTRAINT CK_PortalAssignment_Consent CHECK(PublishName=0 OR IsConfirmed=1)
        );
        CREATE INDEX IX_PortalAssignment_Person ON dbo.PortalOrganizationAssignments(ResidentId,IsActive,IsConfirmed);
    END;
    IF OBJECT_ID('dbo.PortalOrganizationMembers','U') IS NULL
    BEGIN
        CREATE TABLE dbo.PortalOrganizationMembers (
            Id bigint IDENTITY NOT NULL CONSTRAINT PK_PortalOrganizationMembers PRIMARY KEY,
            OrganizationId bigint NOT NULL, ResidentId bigint NOT NULL, RoleLabel nvarchar(200) NULL,
            JoinedOn date NULL, LeftOn date NULL,
            IsActive bit NOT NULL CONSTRAINT DF_PortalMember_Active DEFAULT 1,
            IsConfirmed bit NOT NULL CONSTRAINT DF_PortalMember_Confirmed DEFAULT 0,
            CONSTRAINT FK_PortalMember_Org FOREIGN KEY(OrganizationId) REFERENCES dbo.PortalOrganizations(Id),
            CONSTRAINT FK_PortalMember_Resident FOREIGN KEY(ResidentId) REFERENCES dbo.Residents(Id),
            CONSTRAINT UQ_PortalMember_Person UNIQUE(OrganizationId,ResidentId),
            CONSTRAINT CK_PortalMember_Dates CHECK(JoinedOn IS NULL OR LeftOn IS NULL OR LeftOn>=JoinedOn)
        );
        CREATE INDEX IX_PortalMember_Person ON dbo.PortalOrganizationMembers(ResidentId,IsActive,IsConfirmed);
    END;

    DECLARE @Organizations TABLE(Code varchar(40) PRIMARY KEY,Name nvarchar(200),Kind varchar(24),SourceNote nvarchar(1000));
    INSERT @Organizations VALUES
      ('CHI_BO',N'Chi bộ Đảng','PARTY',NULL),
      ('LANH_DAO_THON',N'Ban lãnh đạo thôn','LOCAL_GOVERNMENT',N'Tên gọi theo tài liệu: Trưởng thôn, Phó Trưởng thôn. Khi áp dụng tại tổ dân phố cần xác nhận tên gọi; chưa tự đồng nhất với Tổ trưởng tổ dân phố.'),
      ('MAT_TRAN',N'Ban công tác Mặt trận','FRONT',N'Tài liệu chưa xác định số lượng Trưởng ban.'),
      ('THANH_NIEN',N'Đoàn Thanh niên','ASSOCIATION',N'Tài liệu chưa xác định số lượng Bí thư Chi đoàn.'),
      ('NONG_DAN',N'Hội Nông dân','ASSOCIATION',N'Tài liệu chưa xác định số lượng Chi hội trưởng.'),
      ('PHU_NU',N'Hội Phụ nữ','ASSOCIATION',N'Tài liệu chưa xác định số lượng Chi hội trưởng.'),
      ('CUU_CHIEN_BINH',N'Hội Cựu chiến binh','ASSOCIATION',N'Tài liệu chưa xác định số lượng Chi hội trưởng.'),
      ('NGUOI_CAO_TUOI',N'Chi hội Người cao tuổi','ASSOCIATION',N'Tài liệu chưa làm rõ chức danh người đứng đầu và số lượng nhân sự; không tự bổ sung chức danh đứng đầu.'),
      ('AN_NINH',N'Tổ an ninh cơ sở','SECURITY',N'Tài liệu chưa mô tả chức danh Tổ trưởng và số lượng cấp phó; không tự bổ sung chức danh đứng đầu.'),
      ('DAN_VU',N'Đoàn thể xã hội về dân vũ','SOCIAL',N'Tài liệu chưa nêu chức danh, số lượng thành viên hoặc cơ cấu lãnh đạo.');
    INSERT dbo.PortalOrganizations(ResidentialGroupId,Code,Name,Kind,SourceNote)
        SELECT @GroupId,s.Code,s.Name,s.Kind,s.SourceNote FROM @Organizations s
        WHERE NOT EXISTS(SELECT 1 FROM dbo.PortalOrganizations o WHERE o.ResidentialGroupId=@GroupId AND o.Code=s.Code);

    DECLARE @Positions TABLE(OrgCode varchar(40),Code varchar(40),Name nvarchar(200),MinCount int NULL,MaxCount int NULL,
        NeedsClarification bit,SourceText nvarchar(500),SourceNote nvarchar(1000),PRIMARY KEY(OrgCode,Code));
    INSERT @Positions VALUES
      ('CHI_BO','BI_THU',N'Bí thư chi bộ',1,1,0,N'1 bí thư : đảng',NULL),
      ('CHI_BO','PHO_BI_THU',N'Phó Bí thư chi bộ',2,3,0,N'Phó bị thư chi bộ: 2- 3 ông',NULL),
      ('LANH_DAO_THON','TRUONG_THON',N'Trưởng thôn',1,1,1,N'1 trưởng thôn: chính quyền',N'Giữ tên chức danh theo tài liệu; cần đối chiếu tên gọi tại tổ dân phố.'),
      ('LANH_DAO_THON','PHO_TRUONG_THON',N'Phó Trưởng thôn',2,3,1,N'Phó trưởng thôn: 2 – 3 ông',N'Giữ tên chức danh theo tài liệu; cần đối chiếu tên gọi tại tổ dân phố.'),
      ('MAT_TRAN','TRUONG_BAN',N'Trưởng ban công tác Mặt trận',NULL,NULL,0,N'Trưởng ban công tác mặt trận',N'Chưa xác định số lượng.'),
      ('MAT_TRAN','PHO_TRUONG_BAN',N'Phó Trưởng ban công tác Mặt trận',2,2,0,N'Phó trưởng ban công tác : 2 ông',NULL),
      ('THANH_NIEN','BI_THU',N'Bí thư Chi đoàn Thanh niên',NULL,NULL,0,N'Bí thư chi đoàn thanh niên:',N'Chưa xác định số lượng.'),
      ('THANH_NIEN','PHO_BI_THU',N'Phó Bí thư Chi đoàn',2,2,0,N'Phó bi thư : 2 ông',NULL),
      ('NONG_DAN','CHI_HOI_TRUONG',N'Chi hội trưởng Hội Nông dân',NULL,NULL,0,N'Chi hội trưởng hội nông dân,',N'Chưa xác định số lượng.'),
      ('NONG_DAN','CHI_HOI_PHO',N'Chi hội phó Hội Nông dân',3,3,0,N'Chi hội phó nông dân: 3 ông',NULL),
      ('PHU_NU','CHI_HOI_TRUONG',N'Chi hội trưởng Hội Phụ nữ',NULL,NULL,0,N'Chi trưởng hội phụ nữ',N'Chưa xác định số lượng; tên chức danh theo danh sách đã được chủ đề tài làm rõ.'),
      ('PHU_NU','PHO_CHI_HOI_TRUONG',N'Phó Chi hội trưởng Hội Phụ nữ',3,3,0,N'Phó chi hội trưởng hội phụ nữ: 3 bà',NULL),
      ('CUU_CHIEN_BINH','CHI_HOI_TRUONG',N'Chi hội trưởng Hội Cựu chiến binh',NULL,NULL,0,N'Chi hội trưởng cựu chiến binh:',N'Chưa xác định số lượng.'),
      ('CUU_CHIEN_BINH','CHI_HOI_PHO',N'Chi hội phó Hội Cựu chiến binh',3,3,0,N'Phó chi hội : 3 cụ',NULL),
      ('NGUOI_CAO_TUOI','PHO_CHI_HOI',N'Phó Chi hội Người cao tuổi',NULL,NULL,1,N'Phó chi hội người cao tuổi',N'Tài liệu chưa làm rõ tên chức danh đầy đủ và số lượng nhân sự.'),
      ('AN_NINH','PHO_TO',N'Phó Tổ trưởng/Tổ phó',NULL,NULL,1,N'Phó tổ trưởng tổ phó',N'Tài liệu chưa làm rõ cách gọi chức danh và số lượng cấp phó.');
    INSERT dbo.PortalOrganizationPositions(OrganizationId,Code,Name,MinimumCount,MaximumCount,NeedsClarification,SourceText,SourceNote)
        SELECT o.Id,s.Code,s.Name,s.MinCount,s.MaxCount,s.NeedsClarification,s.SourceText,s.SourceNote FROM @Positions s
        JOIN dbo.PortalOrganizations o ON o.ResidentialGroupId=@GroupId AND o.Code=s.OrgCode
        WHERE NOT EXISTS(SELECT 1 FROM dbo.PortalOrganizationPositions p WHERE p.OrganizationId=o.Id AND p.Code=s.Code);
    IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='PORTAL-ORG-1.0')
        INSERT dbo.SchemaVersions(VersionCode) VALUES('PORTAL-ORG-1.0');
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;

-- Post-deployment checks. Expected seeded catalog: 10 organizations / 16 positions; no fabricated people.
SELECT g.Id AS GroupId,g.Code,o.Id AS OrganizationId,o.Code,o.Name,o.Kind,o.IsActive,o.IsPublic
FROM dbo.PortalOrganizations o JOIN dbo.ResidentialUnits g ON g.Id=o.ResidentialGroupId WHERE g.Code='$(PortalGroupCode)' ORDER BY o.Id;
SELECT o.Name AS Organization,p.Name,p.MinimumCount,p.MaximumCount,p.NeedsClarification,p.SourceNote
FROM dbo.PortalOrganizationPositions p JOIN dbo.PortalOrganizations o ON o.Id=p.OrganizationId
JOIN dbo.ResidentialUnits g ON g.Id=o.ResidentialGroupId WHERE g.Code='$(PortalGroupCode)' ORDER BY o.Id,p.Id;
SELECT (SELECT COUNT(*) FROM dbo.PortalOrganizationAssignments) AS ActualAssignments,
       (SELECT COUNT(*) FROM dbo.PortalOrganizationMembers) AS ActualMemberships;
GO
