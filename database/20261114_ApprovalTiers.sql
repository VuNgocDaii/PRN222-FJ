/* APPROVAL-TIERS-4.0: duyệt theo cấp.
   - ORGANIZATION_HEAD được phép duyệt/gỡ (phạm vi kiểm soát thêm ở tầng service: chỉ bài ORGANIZATION của tổ chức mình).
   - Nới bộ lọc trong vw_EffectivePermissions; thôi dùng COMMUNE_OFFICER. */
SET XACT_ABORT ON;
BEGIN TRAN;
INSERT dbo.RolePermissions(RoleId,PermissionId)
SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
WHERE r.Name='ORGANIZATION_HEAD' AND p.Code IN('announcements.approve','announcements.unpublish')
AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);

DECLARE @def nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.vw_EffectivePermissions'));
DECLARE @old nvarchar(400)=N'r.Name IN(''VILLAGE_HEAD'',''PARTY_SECRETARY'') AND a.OrganizationId IS NULL)';
DECLARE @new nvarchar(400)=N'r.Name IN(''VILLAGE_HEAD'',''PARTY_SECRETARY'',''ORGANIZATION_HEAD''))';
IF CHARINDEX(@old,@def)>0
BEGIN
    SET @def=REPLACE(@def,@old,@new);
    SET @def=STUFF(@def,1,CHARINDEX(N'VIEW',@def)-1,N'CREATE OR ALTER ');
    EXEC(@def);
END
ELSE IF CHARINDEX(@new,@def)=0
    THROW 51010,'Khong tim thay dieu kien loc trong vw_EffectivePermissions; kiem tra thu cong.',1;

UPDATE a SET RevokedAt=SYSUTCDATETIME() FROM dbo.UserRoleAssignments a JOIN dbo.AspNetRoles r ON r.Id=a.RoleId WHERE r.Name='COMMUNE_OFFICER' AND a.RevokedAt IS NULL;
UPDATE dbo.AspNetRoles SET IsActive=0 WHERE Name='COMMUNE_OFFICER';
UPDATE dbo.AspNetUsers SET PermissionVersion=PermissionVersion+1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='APPROVAL-TIERS-4.0')
    INSERT dbo.SchemaVersions(VersionCode) VALUES('APPROVAL-TIERS-4.0');
COMMIT;
GO
