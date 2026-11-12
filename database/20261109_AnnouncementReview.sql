-- Idempotent upgrade. Existing publications remain visible; draft content is not auto-approved.
USE [QuanLyCuDan];
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='COMPACT-2.0')
    THROW 51001, 'COMPACT-2.0 is required.', 1;
IF COL_LENGTH('dbo.Announcements','ReviewStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Announcements ADD
        ReviewStatus varchar(15) NOT NULL CONSTRAINT DF_Announcements_ReviewStatus DEFAULT 'DRAFT',
        SubmittedAt datetime2(3) NULL,
        ReviewedByUserId nvarchar(450) NULL,
        ReviewedAt datetime2(3) NULL,
        ReviewNote nvarchar(1000) NULL;
    EXEC(N'UPDATE dbo.Announcements SET ReviewStatus=''APPROVED'', SubmittedAt=PublishedAt,
        ReviewedAt=PublishedAt, ReviewedByUserId=PublishedByUserId
        WHERE Status IN (''PUBLISHED'',''UNPUBLISHED'') AND PublishedByUserId IS NOT NULL AND PublishedAt IS NOT NULL;');
    EXEC(N'ALTER TABLE dbo.Announcements ADD CONSTRAINT CK_Announcements_ReviewStatus CHECK(ReviewStatus IN(''DRAFT'',''PENDING'',''APPROVED'',''REJECTED'')),
        CONSTRAINT FK_Announcements_Reviewer FOREIGN KEY(ReviewedByUserId) REFERENCES dbo.AspNetUsers(Id);');
END;
IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE Code='announcements.approve')
    INSERT dbo.Permissions(Code,Name,Module) VALUES('announcements.approve',N'Duyệt hoặc từ chối thông báo','ANNOUNCEMENTS');
DECLARE @PermissionId int=(SELECT Id FROM dbo.Permissions WHERE Code='announcements.approve');
INSERT dbo.PermissionAllowedScopes(PermissionId,ScopeType)
    SELECT @PermissionId,s.ScopeType FROM (VALUES('GROUP'),('AREA')) s(ScopeType)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PermissionAllowedScopes p WHERE p.PermissionId=@PermissionId AND p.ScopeType=s.ScopeType);
INSERT dbo.RolePermissions(RoleId,PermissionId)
    SELECT Id,@PermissionId FROM dbo.AspNetRoles r WHERE r.Name='OFFICER'
    AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions p WHERE p.RoleId=r.Id AND p.PermissionId=@PermissionId);
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='ANNOUNCEMENT-REVIEW')
    INSERT dbo.SchemaVersions(VersionCode) VALUES('ANNOUNCEMENT-REVIEW');
COMMIT;
GO
