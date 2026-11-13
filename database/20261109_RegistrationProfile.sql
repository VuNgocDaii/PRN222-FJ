-- Additive upgrade for an existing COMPACT-2.0 database. No accounts or resident records are changed.
USE [QuanLyCuDan];
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='COMPACT-2.0')
    THROW 51001, 'COMPACT-2.0 is required.', 1;
IF COL_LENGTH('dbo.AspNetUsers', 'RegistrationProfile') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD RegistrationProfile nvarchar(4000) NULL;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='REGISTRATION-PROFILE')
    INSERT dbo.SchemaVersions(VersionCode) VALUES('REGISTRATION-PROFILE');
COMMIT;
GO
