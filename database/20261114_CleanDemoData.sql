/* Don rac du lieu demo cua cac module da bo (Cu tru, Phi), thong bao mau cua to A/B va cac don vi da gop.
   KHONG HOAN TAC DUOC -> BACKUP DB TRUOC. Chay SAU 20261114_SingleVillage.sql. Loi => rollback toan bo.
   Giu lai: tai khoan demo, ho/cu dan, to chuc, phan anh (PA-*), thong bao cua thon. */
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='SINGLE-VILLAGE-4.1')
  THROW 50010,N'Hay chay 20261114_SingleVillage.sql truoc.',1;
IF OBJECT_ID('dbo.usp_TmpCascadeDelete') IS NOT NULL DROP PROC dbo.usp_TmpCascadeDelete;
GO
-- Xoa cac dong thoa @where trong @tbl va moi dong con phu thuoc (de quy qua khoa ngoai).
CREATE PROC dbo.usp_TmpCascadeDelete @tbl sysname, @where nvarchar(max), @depth int=0 AS
BEGIN
  IF @depth>8 THROW 50011,N'De quy qua sau.',1;
  DECLARE @child sysname,@ccol sysname,@pcol sysname,@sql nvarchar(max),@w nvarchar(max);
  DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT OBJECT_NAME(fk.parent_object_id),cc.name,pc.name
    FROM sys.foreign_key_columns fk
    JOIN sys.columns cc ON cc.object_id=fk.parent_object_id AND cc.column_id=fk.parent_column_id
    JOIN sys.columns pc ON pc.object_id=fk.referenced_object_id AND pc.column_id=fk.referenced_column_id
    WHERE fk.referenced_object_id=OBJECT_ID('dbo.'+@tbl) AND fk.parent_object_id<>fk.referenced_object_id;
  OPEN c; FETCH NEXT FROM c INTO @child,@ccol,@pcol;
  WHILE @@FETCH_STATUS=0
  BEGIN
    SET @w=QUOTENAME(@ccol)+N' IN (SELECT '+QUOTENAME(@pcol)+N' FROM dbo.'+QUOTENAME(@tbl)+N' WHERE '+@where+N')';
    SET @depth+=1; EXEC dbo.usp_TmpCascadeDelete @child,@w,@depth; SET @depth-=1;
    FETCH NEXT FROM c INTO @child,@ccol,@pcol;
  END
  CLOSE c; DEALLOCATE c;
  SET @sql=N'DELETE FROM dbo.'+QUOTENAME(@tbl)+N' WHERE '+@where;
  EXEC sys.sp_executesql @sql;
END
GO
BEGIN TRY
BEGIN TRAN;
-- 1) Yeu cau cu tru (giu phan anh)
EXEC dbo.usp_TmpCascadeDelete 'Requests',N'RequestKind=''RESIDENCE''';
-- 2) Toan bo module Phi
EXEC dbo.usp_TmpCascadeDelete 'FeeCampaigns',N'1=1';
-- 3) Thong bao mau cua to A/B
EXEC dbo.usp_TmpCascadeDelete 'Announcements',N'CreatedByUserId IN (''u-officer-a'',''u-officer-b'')';
-- 4) Don vi da bi an sau khi gop (khong con du lieu tham chieu; neu con thi loi + rollback)
DELETE FROM dbo.ResidentialUnits WHERE UnitType='GROUP' AND IsActive=0;
DELETE FROM dbo.ResidentialUnits WHERE UnitType='AREA' AND IsActive=0;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='CLEAN-DEMO-4.2')
  INSERT dbo.SchemaVersions(VersionCode) VALUES('CLEAN-DEMO-4.2');
COMMIT;
SELECT N'Requests' AS T,COUNT(*) N FROM dbo.Requests UNION ALL SELECT N'Announcements',COUNT(*) FROM dbo.Announcements
UNION ALL SELECT N'Units',COUNT(*) FROM dbo.ResidentialUnits;
END TRY
BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
END CATCH
GO
DROP PROC dbo.usp_TmpCascadeDelete;
