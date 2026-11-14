/* Gộp về MỘT thôn: giữ Xã Minh An (AREA) + Thôn An Hòa (GROUP).
   - Mọi dữ liệu đang trỏ tới thôn/tổ/khu khác được chuyển sang thôn An Hòa.
   - Các đơn vị còn lại chỉ bị ẩn (IsActive=0), không xóa.
   - Chạy 1 lần trên DB QuanLyCuDan_Refactor, sau 20261114_ApprovalTiers.sql. Lỗi => rollback toàn bộ. */
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRAN;
DECLARE @area bigint=(SELECT Id FROM dbo.ResidentialUnits WHERE Code='XA-MINH-AN' AND UnitType='AREA');
DECLARE @grp  bigint=(SELECT Id FROM dbo.ResidentialUnits WHERE Code='THON-AN-HOA' AND UnitType='GROUP');
IF @area IS NULL OR @grp IS NULL THROW 50001,N'Không thấy XA-MINH-AN / THON-AN-HOA.',1;

DECLARE @sql nvarchar(max)=N'';
SELECT @sql+=N'UPDATE t SET t.'+QUOTENAME(c.name)+N'=CASE u.UnitType WHEN ''AREA'' THEN '+CAST(@area AS nvarchar(20))
  +N' ELSE '+CAST(@grp AS nvarchar(20))+N' END FROM '+QUOTENAME(s.name)+N'.'+QUOTENAME(o.name)+N' t JOIN dbo.ResidentialUnits u ON u.Id=t.'
  +QUOTENAME(c.name)+N' WHERE u.Id NOT IN('+CAST(@area AS nvarchar(20))+N','+CAST(@grp AS nvarchar(20))+N');'+CHAR(10)
FROM sys.foreign_key_columns fk
JOIN sys.objects o ON o.object_id=fk.parent_object_id JOIN sys.schemas s ON s.schema_id=o.schema_id
JOIN sys.columns c ON c.object_id=fk.parent_object_id AND c.column_id=fk.parent_column_id
WHERE fk.referenced_object_id=OBJECT_ID('dbo.ResidentialUnits')
  AND NOT (o.name='ResidentialUnits' AND c.name='ParentUnitId');
EXEC sys.sp_executesql @sql;

UPDATE dbo.ResidentialUnits SET IsActive=1 WHERE Id IN(@area,@grp);
UPDATE dbo.ResidentialUnits SET IsActive=0 WHERE Id NOT IN(@area,@grp);
UPDATE dbo.ResidentialUnits SET Name=N'Thôn An Hòa' WHERE Id=@grp;
UPDATE dbo.AspNetUsers SET PermissionVersion=PermissionVersion+1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='SINGLE-VILLAGE-4.1')
  INSERT dbo.SchemaVersions(VersionCode) VALUES('SINGLE-VILLAGE-4.1');
COMMIT;
SELECT UnitType,Code,Name,IsActive FROM dbo.ResidentialUnits ORDER BY Id;
END TRY
BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
END CATCH
