/* FULL INSTALLER AND ADDITIVE MIGRATION: base schema, village features, seed accounts/data.
Run the WHOLE file with sqlcmd -b -f 65001 or SSMS. Never drops an existing database.
All 17 known seed accounts use 12345678; VILLAGE-DATA-3.2 normalizes their passwords once.
Other account passwords are retained. */
/*
  QUAN LY CU DAN - COMPACT MVP v2.0 - SQL SERVER 2019+
  8 BUSINESS TABLES + 14 AUTH/AUTHORIZATION TABLES + 1 VERSION TABLE = 23 TABLES.
  Database name: QuanLyCuDan.
  Run WHOLE file in SSMS or:
    sqlcmd -S .\SQLEXPRESS -E -b -f 65001 -i QuanLyCuDan.sql
  Accounts: admin, canbo01, canbo02, thuphi01, dan01, dan02, dan03, pending01.
  All passwords: 12345678  All people, ID numbers and addresses are FICTIONAL.

  Business: ResidentialUnits, Households, Residents, Requests, Activities,
            FeeCampaigns, Payments, Announcements.
  - Units: AREA roots and GROUP children; no multi-level administrative hierarchy.
  - A resident has ONE current household; old/new state is frozen on approved Requests.
  - Requests store residence requests OR private feedback; Activities store timelines,
    messages, safe audit metadata and per-recipient notification delivery/read state.
  - One optional private attachment per request. SQL does not create physical files.
  - Fees are per household; one assessment/result row per campaign+household.
    Only full payment once: no partial payment, refunds, personal fees or ledger.
  - Source STAFF requests record direct authorized changes; no made-up resident request.

  PasswordHash: SHA256$<salt-16-byte hex>$<hash-32-byte hex>.
  SHA256(saltBytes || UTF16LE(password)), no BOM, no Trim, random salt per user.
  Use companion DemoSha256PasswordHasher.cs, NOT Identity's default PBKDF2 verifier.
  SHA256 is demo-only as requested; use a slow password KDF for real passwords.

  Safe rerun: completed COMPACT-2.0 => apply additive upgrades in subsequent batches. Nonempty old/full/other database
  => fail without DROP/DELETE. Existing COMPACT-2.0 data/passwords are preserved; other schemas require a separate migration.
  If QuanLyCuDan already has v1 tables, back up and plan migration separately.
  No usable sessions, refresh tokens, reset tokens or real attachments are seeded.
  Role source = UserRoleAssignments; standard AspNetUserRoles stays empty.
  All datetime2 timestamps are UTC. API must enforce scope, ownership and workflow.
*/
USE [master];
GO
IF DB_ID(N'QuanLyCuDan') IS NULL EXEC(N'CREATE DATABASE [QuanLyCuDan]');
GO
USE [QuanLyCuDan];
GO
IF DB_NAME()<>N'QuanLyCuDan' THROW 51002, 'Wrong database context. Stop before creating tables.',1;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'QuanLyCuDan.Schema.Compact2',
         @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51000, 'Could not acquire schema initialization lock.',1;
    IF OBJECT_ID(N'dbo.SchemaVersions',N'U') IS NOT NULL
    BEGIN
        DECLARE @Installed bit=0;
        EXEC sys.sp_executesql
          N'SELECT @Done=CONVERT(bit,COUNT(*)) FROM dbo.SchemaVersions WHERE VersionCode=N''COMPACT-2.0'';',
          N'@Done bit OUTPUT',@Done=@Installed OUTPUT;
        IF @Installed=1
        BEGIN
            COMMIT;
            PRINT N'Compact v2.0 already installed. Continuing with additive upgrades; data/passwords preserved.';
            RETURN;
        END;
    END;
    IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
        THROW 51001, 'Database is not empty or uses the old/full schema. No objects were deleted. This file requires a new empty database.',1;
    CREATE TABLE dbo.SchemaVersions (
        VersionCode varchar(20) NOT NULL PRIMARY KEY,
        InstalledAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    -- IDENTITY-COMPATIBLE USERS/ROLES + STANDARD AUXILIARY TABLES
    CREATE TABLE dbo.AspNetUsers (
        Id nvarchar(450) NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
        UserName nvarchar(256) NOT NULL, NormalizedUserName nvarchar(256) NOT NULL,
        Email nvarchar(256) NULL, NormalizedEmail nvarchar(256) NULL,
        EmailConfirmed bit NOT NULL DEFAULT 0,
        PasswordHash nvarchar(max) NULL, SecurityStamp nvarchar(max) NULL,
        ConcurrencyStamp nvarchar(max) NULL, PhoneNumber nvarchar(max) NULL,
        PhoneNumberConfirmed bit NOT NULL DEFAULT 0,
        TwoFactorEnabled bit NOT NULL DEFAULT 0,
        LockoutEnd datetimeoffset(7) NULL, LockoutEnabled bit NOT NULL DEFAULT 1,
        AccessFailedCount int NOT NULL DEFAULT 0,
        FullName nvarchar(150) NOT NULL, IsActive bit NOT NULL DEFAULT 1,
        ResidentId bigint NULL,
        VerificationStatus varchar(20) NOT NULL DEFAULT 'PENDING',
        RegistrationProfile nvarchar(4000) NULL,
        ReviewedByUserId nvarchar(450) NULL, ReviewedAt datetime2(3) NULL,
        RejectionReason nvarchar(1000) NULL,
        PermissionVersion int NOT NULL DEFAULT 1,
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_Users_Verification CHECK (VerificationStatus IN ('PENDING','APPROVED','REJECTED','NOT_APPLICABLE')),
        CONSTRAINT CK_Users_ReviewReason CHECK (VerificationStatus<>'REJECTED' OR LEN(LTRIM(RTRIM(RejectionReason)))>0 AND RejectionReason IS NOT NULL),
        CONSTRAINT FK_Users_Reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.AspNetUsers(Id)
    );
    CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers(NormalizedUserName);
    CREATE INDEX EmailIndex ON dbo.AspNetUsers(NormalizedEmail);
    CREATE UNIQUE INDEX UX_Users_Resident ON dbo.AspNetUsers(ResidentId) WHERE ResidentId IS NOT NULL;
    CREATE TABLE dbo.AspNetRoles (
        Id nvarchar(450) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
        Name nvarchar(256) NOT NULL, NormalizedName nvarchar(256) NOT NULL,
        ConcurrencyStamp nvarchar(max) NULL,
        DisplayName nvarchar(150) NOT NULL, Description nvarchar(500) NULL,
        IsSystemRole bit NOT NULL DEFAULT 0, IsActive bit NOT NULL DEFAULT 1
    );
    CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles(NormalizedName);
    CREATE TABLE dbo.AspNetUserClaims (
        Id int IDENTITY PRIMARY KEY, UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        ClaimType nvarchar(max) NULL, ClaimValue nvarchar(max) NULL
    );
    CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims(UserId);
    CREATE TABLE dbo.AspNetRoleClaims (
        Id int IDENTITY PRIMARY KEY, RoleId nvarchar(450) NOT NULL REFERENCES dbo.AspNetRoles(Id),
        ClaimType nvarchar(max) NULL, ClaimValue nvarchar(max) NULL
    );
    CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims(RoleId);
    CREATE TABLE dbo.AspNetUserLogins (
        LoginProvider nvarchar(128) NOT NULL, ProviderKey nvarchar(128) NOT NULL,
        ProviderDisplayName nvarchar(max) NULL,
        UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        PRIMARY KEY(LoginProvider,ProviderKey)
    );
    CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins(UserId);
    CREATE TABLE dbo.AspNetUserTokens (
        UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        LoginProvider nvarchar(128) NOT NULL, Name nvarchar(128) NOT NULL, Value nvarchar(max) NULL,
        PRIMARY KEY(UserId,LoginProvider,Name)
    );
    CREATE TABLE dbo.AspNetUserRoles (
        UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        RoleId nvarchar(450) NOT NULL REFERENCES dbo.AspNetRoles(Id), PRIMARY KEY(UserId,RoleId)
    );
    CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles(RoleId);

    CREATE TABLE dbo.ResidentialUnits (
        Id bigint IDENTITY CONSTRAINT PK_ResidentialUnits PRIMARY KEY,
        Code varchar(30) NOT NULL UNIQUE, Name nvarchar(200) NOT NULL,
        UnitType varchar(10) NOT NULL CHECK(UnitType IN ('AREA','GROUP')),
        ParentUnitId bigint NULL REFERENCES dbo.ResidentialUnits(Id),
        Address nvarchar(300) NULL, IsActive bit NOT NULL DEFAULT 1,
        RowVersion rowversion NOT NULL,
        CHECK(UnitType='AREA' AND ParentUnitId IS NULL OR UnitType='GROUP' AND ParentUnitId IS NOT NULL),
        CHECK(ParentUnitId IS NULL OR ParentUnitId<>Id)
    );
    CREATE INDEX IX_Units_Parent ON dbo.ResidentialUnits(ParentUnitId,UnitType);

    CREATE TABLE dbo.Permissions (
        Id int IDENTITY PRIMARY KEY, Code varchar(100) NOT NULL UNIQUE,
        Name nvarchar(200) NOT NULL, Module varchar(50) NOT NULL,
        Description nvarchar(500) NULL
    );
    CREATE TABLE dbo.PermissionAllowedScopes (
        PermissionId int NOT NULL REFERENCES dbo.Permissions(Id),
        ScopeType varchar(10) NOT NULL CHECK (ScopeType IN ('SYSTEM','AREA','GROUP','SELF')),
        PRIMARY KEY(PermissionId,ScopeType)
    );
    CREATE TABLE dbo.RolePermissions (
        RoleId nvarchar(450) NOT NULL REFERENCES dbo.AspNetRoles(Id),
        PermissionId int NOT NULL REFERENCES dbo.Permissions(Id), PRIMARY KEY(RoleId,PermissionId)
    );
    CREATE INDEX IX_RolePermissions_Permission ON dbo.RolePermissions(PermissionId);
    CREATE TABLE dbo.UserRoleAssignments (
        Id bigint IDENTITY PRIMARY KEY,
        UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        RoleId nvarchar(450) NOT NULL REFERENCES dbo.AspNetRoles(Id),
        ScopeType varchar(10) NOT NULL,
        ResidentialAreaId bigint NULL REFERENCES dbo.ResidentialUnits(Id),
        ResidentialGroupId bigint NULL REFERENCES dbo.ResidentialUnits(Id),
        ValidFrom datetime2(3) NOT NULL, ValidUntil datetime2(3) NULL,
        RevokedAt datetime2(3) NULL, RevocationReason nvarchar(500) NULL,
        AssignedByUserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        AssignedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
        CONSTRAINT CK_Assignments_Scope CHECK (
            ScopeType IN ('SYSTEM','SELF') AND ResidentialAreaId IS NULL AND ResidentialGroupId IS NULL OR
            ScopeType='AREA' AND ResidentialAreaId IS NOT NULL AND ResidentialGroupId IS NULL OR
            ScopeType='GROUP' AND ResidentialAreaId IS NULL AND ResidentialGroupId IS NOT NULL),
        CONSTRAINT CK_Assignments_Dates CHECK (ValidUntil IS NULL OR ValidUntil>ValidFrom)
    );
    CREATE INDEX IX_Assignments_User ON dbo.UserRoleAssignments(UserId,RevokedAt,ValidFrom,ValidUntil);
    CREATE INDEX IX_Assignments_Group ON dbo.UserRoleAssignments(ResidentialGroupId,RoleId);


    -- BUSINESS TABLE 2: current household; head must belong to this very household.
    CREATE TABLE dbo.Households (
        Id bigint IDENTITY PRIMARY KEY, Code varchar(30) NOT NULL UNIQUE,
        ResidentialGroupId bigint NOT NULL REFERENCES dbo.ResidentialUnits(Id),
        Address nvarchar(300) NOT NULL, RegisteredAt date NOT NULL,
        HeadResidentId bigint NULL, IsArchived bit NOT NULL DEFAULT 0,
        CreatedByUserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL
    );
    CREATE INDEX IX_Households_Group ON dbo.Households(ResidentialGroupId,IsArchived);
    -- BUSINESS TABLE 3: current state only; change history lives in approved Requests.
    CREATE TABLE dbo.Residents (
        Id bigint IDENTITY PRIMARY KEY, FullName nvarchar(150) NOT NULL,
        CitizenId varchar(12) NULL, DateOfBirth date NOT NULL,
        Gender varchar(10) NOT NULL CHECK(Gender IN ('MALE','FEMALE','OTHER')),
        PhoneNumber varchar(20) NULL, Occupation nvarchar(150) NULL,
        HouseholdId bigint NULL REFERENCES dbo.Households(Id), RelationshipToHead nvarchar(50) NULL,
        ResidenceStatus varchar(20) NOT NULL CHECK(ResidenceStatus IN ('RESIDING','TEMPORARY','ABSENT','MOVED_OUT')),
        MovedInAt date NULL, MovedOutAt date NULL,
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
        CONSTRAINT UQ_Residents_IdHousehold UNIQUE(Id,HouseholdId),
        CHECK(CitizenId IS NULL OR LEN(CitizenId)=12 AND CitizenId NOT LIKE '%[^0-9]%'),
        CHECK(ResidenceStatus='MOVED_OUT' AND HouseholdId IS NULL AND RelationshipToHead IS NULL
          OR ResidenceStatus<>'MOVED_OUT' AND HouseholdId IS NOT NULL AND RelationshipToHead IS NOT NULL),
        CHECK(MovedOutAt IS NULL OR MovedInAt IS NULL OR MovedOutAt>=MovedInAt)
    );
    CREATE UNIQUE INDEX UX_Residents_CitizenId ON dbo.Residents(CitizenId) WHERE CitizenId IS NOT NULL;
    CREATE INDEX IX_Residents_Name ON dbo.Residents(FullName);
    CREATE INDEX IX_Residents_Phone ON dbo.Residents(PhoneNumber);
    CREATE INDEX IX_Residents_Household ON dbo.Residents(HouseholdId,ResidenceStatus);
    ALTER TABLE dbo.Households ADD CONSTRAINT FK_Households_HeadMember
      FOREIGN KEY(HeadResidentId,Id) REFERENCES dbo.Residents(Id,HouseholdId);
    ALTER TABLE dbo.AspNetUsers ADD CONSTRAINT FK_Users_Resident FOREIGN KEY(ResidentId) REFERENCES dbo.Residents(Id);

    -- BUSINESS TABLE 4: common intake plus immutable approved change snapshots.
    CREATE TABLE dbo.Requests (
        Id bigint IDENTITY PRIMARY KEY, Code varchar(30) NOT NULL UNIQUE,
        RequestKind varchar(15) NOT NULL CHECK(RequestKind IN ('RESIDENCE','FEEDBACK')),
        Source varchar(10) NOT NULL DEFAULT 'RESIDENT' CHECK(Source IN ('RESIDENT','STAFF')),
        SubmittedByUserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        ResidentId bigint NULL REFERENCES dbo.Residents(Id),
        AssignedGroupId bigint NOT NULL REFERENCES dbo.ResidentialUnits(Id),
        RequestType varchar(30) NOT NULL, Category varchar(30) NULL,
        Subject nvarchar(250) NOT NULL, Body nvarchar(4000) NOT NULL,
        StartDate date NULL, EndDate date NULL,
        OriginalHouseholdId bigint NULL REFERENCES dbo.Households(Id),
        TargetHouseholdId bigint NULL REFERENCES dbo.Households(Id),
        OriginalResidenceStatus varchar(20) NULL, ResultResidenceStatus varchar(20) NULL,
        OriginalRelationship nvarchar(50) NULL, ResultRelationship nvarchar(50) NULL,
        OldHeadResidentId bigint NULL REFERENCES dbo.Residents(Id),
        NewHeadResidentId bigint NULL REFERENCES dbo.Residents(Id),
        ChangeAppliedAt datetime2(3) NULL,
        Status varchar(20) NOT NULL DEFAULT 'PENDING',
        ProcessedByUserId nvarchar(450) NULL REFERENCES dbo.AspNetUsers(Id),
        ProcessedAt datetime2(3) NULL, ProcessingNote nvarchar(2000) NULL,
        AttachmentStorageKey varchar(100) NULL, AttachmentFileName nvarchar(255) NULL,
        AttachmentContentType varchar(50) NULL, AttachmentSizeBytes bigint NULL,
        AttachmentHash binary(32) NULL,
        ClientRequestKey varchar(64) NOT NULL,
        SubmittedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
        UNIQUE(SubmittedByUserId,ClientRequestKey),
        CHECK(RequestKind='RESIDENCE' AND ResidentId IS NOT NULL AND StartDate IS NOT NULL AND Category IS NULL
          AND RequestType IN ('MOVE_IN','MOVE_OUT','TEMPORARY_RESIDENCE','TEMPORARY_ABSENCE','ADD_MEMBER','CHANGE_HEAD')
          AND Status IN ('PENDING','PROCESSING','APPROVED','REJECTED','CANCELLED')
          OR RequestKind='FEEDBACK' AND Source='RESIDENT' AND RequestType='FEEDBACK'
          AND Category IS NOT NULL AND Category IN ('ENVIRONMENT','INFRASTRUCTURE','OTHER')
          AND StartDate IS NULL AND EndDate IS NULL AND OriginalHouseholdId IS NULL AND TargetHouseholdId IS NULL
          AND OriginalResidenceStatus IS NULL AND ResultResidenceStatus IS NULL
          AND OldHeadResidentId IS NULL AND NewHeadResidentId IS NULL AND ChangeAppliedAt IS NULL
          AND Status IN ('PENDING','PROCESSING','RESPONDED','CLOSED','OUT_OF_SCOPE')),
        CHECK(EndDate IS NULL OR StartDate IS NOT NULL AND EndDate>=StartDate),
        CHECK(Status NOT IN ('APPROVED','REJECTED','RESPONDED','CLOSED','OUT_OF_SCOPE')
          OR ProcessedByUserId IS NOT NULL AND ProcessedAt IS NOT NULL),
        CHECK(Status NOT IN ('REJECTED','OUT_OF_SCOPE') OR ProcessingNote IS NOT NULL AND LEN(LTRIM(RTRIM(ProcessingNote)))>0),
        CHECK(Status<>'APPROVED' OR Source='STAFF' OR ProcessedByUserId<>SubmittedByUserId),
        CHECK(OriginalResidenceStatus IS NULL OR OriginalResidenceStatus IN ('RESIDING','TEMPORARY','ABSENT','MOVED_OUT')),
        CHECK(ResultResidenceStatus IS NULL OR ResultResidenceStatus IN ('RESIDING','TEMPORARY','ABSENT','MOVED_OUT')),
        CHECK(Status<>'APPROVED' OR ChangeAppliedAt IS NOT NULL AND OriginalResidenceStatus IS NOT NULL AND ResultResidenceStatus IS NOT NULL),
        CHECK(Status='APPROVED' OR ChangeAppliedAt IS NULL),
        CHECK(Status<>'APPROVED' OR ResultResidenceStatus='MOVED_OUT' AND TargetHouseholdId IS NULL
          OR ResultResidenceStatus<>'MOVED_OUT' AND TargetHouseholdId IS NOT NULL AND ResultRelationship IS NOT NULL),
        CHECK(Status<>'APPROVED' OR RequestType<>'CHANGE_HEAD' OR NewHeadResidentId IS NOT NULL),
        CHECK(AttachmentStorageKey IS NULL AND AttachmentFileName IS NULL AND AttachmentContentType IS NULL AND AttachmentSizeBytes IS NULL AND AttachmentHash IS NULL
          OR AttachmentStorageKey IS NOT NULL AND AttachmentFileName IS NOT NULL AND AttachmentHash IS NOT NULL
          AND AttachmentContentType IS NOT NULL AND AttachmentContentType IN ('application/pdf','image/jpeg','image/png')
          AND AttachmentSizeBytes IS NOT NULL AND AttachmentSizeBytes BETWEEN 1 AND 5242880)
    );
    CREATE UNIQUE INDEX UX_Requests_Attachment ON dbo.Requests(AttachmentStorageKey) WHERE AttachmentStorageKey IS NOT NULL;
    CREATE INDEX IX_Requests_Inbox ON dbo.Requests(AssignedGroupId,RequestKind,Status,SubmittedAt);
    CREATE INDEX IX_Requests_Mine ON dbo.Requests(SubmittedByUserId,RequestKind,SubmittedAt);
    CREATE INDEX IX_Requests_Changes ON dbo.Requests(ResidentId,Status,ChangeAppliedAt);

    -- BUSINESS TABLE 5: typed shared timeline/messages/audit/notification storage.
    CREATE TABLE dbo.Activities (
        Id bigint IDENTITY PRIMARY KEY,
        ActivityType varchar(20) NOT NULL CHECK(ActivityType IN ('REQUEST_EVENT','MESSAGE','AUDIT','NOTIFICATION')),
        RequestId bigint NULL REFERENCES dbo.Requests(Id),
        ActorUserId nvarchar(450) NULL REFERENCES dbo.AspNetUsers(Id),
        RecipientUserId nvarchar(450) NULL REFERENCES dbo.AspNetUsers(Id),
        EventCode varchar(80) NOT NULL, Body nvarchar(4000) NULL,
        FromStatus varchar(20) NULL, ToStatus varchar(20) NULL,
        FromGroupId bigint NULL REFERENCES dbo.ResidentialUnits(Id),
        ToGroupId bigint NULL REFERENCES dbo.ResidentialUnits(Id),
        TargetType varchar(40) NULL, TargetId varchar(64) NULL,
        SafeMetadata nvarchar(2000) NULL,
        ClientMessageKey varchar(64) NULL,
        OccurredAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ReadAt datetime2(3) NULL, DispatchedAt datetime2(3) NULL,
        AttemptCount int NOT NULL DEFAULT 0 CHECK(AttemptCount>=0), NextAttemptAt datetime2(3) NULL,
        CHECK(SafeMetadata IS NULL OR ISJSON(SafeMetadata)=1),
        CHECK(ActivityType='MESSAGE' AND RequestId IS NOT NULL AND ActorUserId IS NOT NULL
          AND Body IS NOT NULL AND LEN(LTRIM(RTRIM(Body)))>0 AND RecipientUserId IS NULL AND ClientMessageKey IS NOT NULL
          OR ActivityType='REQUEST_EVENT' AND RequestId IS NOT NULL AND ActorUserId IS NOT NULL AND Body IS NULL AND RecipientUserId IS NULL
          OR ActivityType='AUDIT' AND RequestId IS NULL AND Body IS NULL AND RecipientUserId IS NULL
          OR ActivityType='NOTIFICATION' AND RecipientUserId IS NOT NULL AND Body IS NULL AND TargetType IS NOT NULL AND TargetId IS NOT NULL),
        CHECK(ActivityType='NOTIFICATION' OR ReadAt IS NULL AND DispatchedAt IS NULL AND AttemptCount=0 AND NextAttemptAt IS NULL)
    );
    CREATE INDEX IX_Activities_Request ON dbo.Activities(RequestId,OccurredAt);
    CREATE INDEX IX_Activities_Recipient ON dbo.Activities(RecipientUserId,ActivityType,ReadAt,OccurredAt);
    CREATE INDEX IX_Activities_Dispatch ON dbo.Activities(ActivityType,DispatchedAt,NextAttemptAt);
    CREATE UNIQUE INDEX UX_Activities_ClientMessage ON dbo.Activities(ActorUserId,ClientMessageKey) WHERE ClientMessageKey IS NOT NULL;

    -- BUSINESS TABLE 6: one campaign, household-only fees. Amount per household.
    CREATE TABLE dbo.FeeCampaigns (
        Id bigint IDENTITY PRIMARY KEY, Code varchar(30) NOT NULL UNIQUE,
        ResidentialGroupId bigint NOT NULL REFERENCES dbo.ResidentialUnits(Id),
        Name nvarchar(200) NOT NULL, Description nvarchar(1000) NULL,
        FeeType varchar(20) NOT NULL CHECK(FeeType IN ('REQUIRED','VOLUNTARY')),
        DueDate date NULL, AmountPerHousehold decimal(18,2) NOT NULL CHECK(AmountPerHousehold>=0),
        IsActive bit NOT NULL DEFAULT 1,
        CreatedByUserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL
    );
    -- BUSINESS TABLE 7: a due assessment and its one full-payment result in one row.
    CREATE TABLE dbo.Payments (
        Id bigint IDENTITY PRIMARY KEY,
        CampaignId bigint NOT NULL REFERENCES dbo.FeeCampaigns(Id),
        HouseholdId bigint NOT NULL REFERENCES dbo.Households(Id),
        AmountDue decimal(18,2) NOT NULL CHECK(AmountDue>=0),
        IsPaid bit NOT NULL DEFAULT 0, PaidAmount decimal(18,2) NULL,
        ReceiptCode varchar(40) NULL, Method varchar(20) NULL,
        PaidAt datetime2(3) NULL,
        RecordedByUserId nvarchar(450) NULL REFERENCES dbo.AspNetUsers(Id),
        IsCancelled bit NOT NULL DEFAULT 0, CancellationReason nvarchar(1000) NULL,
        ClientPaymentKey varchar(64) NULL,
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
        UNIQUE(CampaignId,HouseholdId),
        CHECK(IsPaid=0 AND PaidAmount IS NULL AND ReceiptCode IS NULL AND Method IS NULL AND PaidAt IS NULL AND RecordedByUserId IS NULL AND ClientPaymentKey IS NULL
          OR IsPaid=1 AND PaidAmount IS NOT NULL AND PaidAmount=AmountDue AND PaidAmount>0
          AND ReceiptCode IS NOT NULL AND Method IS NOT NULL AND Method IN ('CASH','BANK_TRANSFER')
          AND PaidAt IS NOT NULL AND RecordedByUserId IS NOT NULL AND ClientPaymentKey IS NOT NULL),
        CHECK(IsCancelled=0 OR IsPaid=0 AND CancellationReason IS NOT NULL AND LEN(LTRIM(RTRIM(CancellationReason)))>0)
    );
    CREATE UNIQUE INDEX UX_Payments_Receipt ON dbo.Payments(ReceiptCode) WHERE ReceiptCode IS NOT NULL;
    CREATE UNIQUE INDEX UX_Payments_ClientKey ON dbo.Payments(ClientPaymentKey) WHERE ClientPaymentKey IS NOT NULL;
    CREATE INDEX IX_Payments_Household ON dbo.Payments(HouseholdId,IsPaid,IsCancelled);

    -- BUSINESS TABLE 8: current announcement content with moderation; edits require fresh review.
    CREATE TABLE dbo.Announcements (
        Id bigint IDENTITY PRIMARY KEY, ResidentialGroupId bigint NOT NULL REFERENCES dbo.ResidentialUnits(Id),
        Title nvarchar(250) NOT NULL, Body nvarchar(max) NOT NULL,
        Audience varchar(15) NOT NULL CHECK(Audience IN ('PUBLIC','RESIDENTS')),
        Status varchar(15) NOT NULL DEFAULT 'DRAFT' CHECK(Status IN ('DRAFT','PUBLISHED','UNPUBLISHED')),
        ReviewStatus varchar(15) NOT NULL DEFAULT 'DRAFT' CHECK(ReviewStatus IN ('DRAFT','PENDING','APPROVED','REJECTED')),
        SubmittedAt datetime2(3) NULL,
        ReviewedByUserId nvarchar(450) NULL REFERENCES dbo.AspNetUsers(Id),
        ReviewedAt datetime2(3) NULL, ReviewNote nvarchar(1000) NULL,
        CreatedByUserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        PublishedByUserId nvarchar(450) NULL REFERENCES dbo.AspNetUsers(Id),
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), PublishedAt datetime2(3) NULL,
        ExpiresAt datetime2(3) NULL, UnpublishReason nvarchar(1000) NULL, RowVersion rowversion NOT NULL,
        CHECK(Status<>'PUBLISHED' OR PublishedAt IS NOT NULL AND PublishedByUserId IS NOT NULL),
        CHECK(ExpiresAt IS NULL OR PublishedAt IS NULL OR ExpiresAt>PublishedAt)
    );
    CREATE INDEX IX_Announcements_Public ON dbo.Announcements(Status,Audience,PublishedAt);
    CREATE TABLE dbo.UserSessions (
        Id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        DeviceLabel nvarchar(200) NULL, CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        AbsoluteExpiresAt datetime2(3) NOT NULL, RevokedAt datetime2(3) NULL,
        RevocationReason nvarchar(500) NULL, RowVersion rowversion NOT NULL,
        CHECK(AbsoluteExpiresAt>CreatedAt)
    );
    CREATE INDEX IX_Sessions_User ON dbo.UserSessions(UserId,RevokedAt,AbsoluteExpiresAt);
    CREATE TABLE dbo.RefreshTokens (
        Id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SessionId uniqueidentifier NOT NULL REFERENCES dbo.UserSessions(Id),
        TokenHash binary(32) NOT NULL UNIQUE,
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), ExpiresAt datetime2(3) NOT NULL,
        ConsumedAt datetime2(3) NULL, RevokedAt datetime2(3) NULL,
        ReplacedByTokenId uniqueidentifier NULL REFERENCES dbo.RefreshTokens(Id),
        CHECK(ExpiresAt>CreatedAt)
    );
    CREATE INDEX IX_RefreshTokens_Session ON dbo.RefreshTokens(SessionId);
    CREATE TABLE dbo.PasswordResetChallenges (
        Id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id), TokenHash binary(32) NOT NULL UNIQUE,
        CreatedByUserId nvarchar(450) NOT NULL REFERENCES dbo.AspNetUsers(Id),
        CreatedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), ExpiresAt datetime2(3) NOT NULL,
        ConsumedAt datetime2(3) NULL, CHECK(ExpiresAt>CreatedAt)
    );
    -- SEED: credentials are hashed independently with a random per-account salt.
    DECLARE @Now datetime2(3)=SYSUTCDATETIME(), @DemoPassword nvarchar(100)=N'12345678';
    DECLARE @UserSeed TABLE(Id nvarchar(450), UserName nvarchar(256), FullName nvarchar(150), VerificationStatus varchar(20));
    INSERT @UserSeed VALUES
      (N'u-admin',N'admin',N'Nguyễn Thành Trung','NOT_APPLICABLE'),
      (N'u-officer-a',N'canbo01',N'Đặng Văn Hải','NOT_APPLICABLE'),
      (N'u-officer-b',N'canbo02',N'Trần Quốc Hùng','NOT_APPLICABLE'),
      (N'u-collector',N'thuphi01',N'Phạm Thị Hồng','NOT_APPLICABLE'),
      (N'u-resident-a',N'dan01',N'Nguyễn Văn An','APPROVED'),
      (N'u-resident-b',N'dan02',N'Trần Thị Bình','APPROVED'),
      (N'u-resident-c',N'dan03',N'Lê Văn Cường','APPROVED'),
      (N'u-pending',N'pending01',N'Đỗ Thị Hương','PENDING');
    DECLARE @Uid nvarchar(450), @Username nvarchar(256), @Name nvarchar(150), @Verification varchar(20), @Salt varbinary(16);
    DECLARE UserCursor CURSOR LOCAL FAST_FORWARD FOR SELECT Id,UserName,FullName,VerificationStatus FROM @UserSeed;
    OPEN UserCursor;
    FETCH NEXT FROM UserCursor INTO @Uid,@Username,@Name,@Verification;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Salt=CRYPT_GEN_RANDOM(16);
        INSERT dbo.AspNetUsers(Id,UserName,NormalizedUserName,Email,NormalizedEmail,PasswordHash,SecurityStamp,ConcurrencyStamp,FullName,VerificationStatus)
        VALUES(@Uid,@Username,UPPER(@Username),@Username+N'@example.invalid',UPPER(@Username+N'@example.invalid'),
          N'SHA256$'+CONVERT(varchar(32),@Salt,2)+N'$'+CONVERT(varchar(64),HASHBYTES('SHA2_256',@Salt+CONVERT(varbinary(max),@DemoPassword)),2),
          CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),@Name,@Verification);
        FETCH NEXT FROM UserCursor INTO @Uid,@Username,@Name,@Verification;
    END;
    CLOSE UserCursor;
    DEALLOCATE UserCursor;

    INSERT dbo.AspNetRoles(Id,Name,NormalizedName,DisplayName,Description,IsSystemRole,ConcurrencyStamp) VALUES
      (N'r-admin',N'ADMIN',N'ADMIN',N'Quản trị',N'Quản trị hệ thống; không mặc nhiên đọc dữ liệu nghiệp vụ.',1,CONVERT(nvarchar(36),NEWID())),
      (N'r-officer',N'OFFICER',N'OFFICER',N'Cán bộ',N'Nghiệp vụ trong địa bàn được phân công.',1,CONVERT(nvarchar(36),NEWID())),
      (N'r-resident',N'RESIDENT',N'RESIDENT',N'Cư dân',N'Dữ liệu cá nhân/hộ theo quan hệ đã xác minh.',1,CONVERT(nvarchar(36),NEWID())),
      (N'r-collector',N'FEE_COLLECTOR',N'FEE_COLLECTOR',N'Cán bộ thu phí',N'Ví dụ role tùy chỉnh chỉ được đọc/ghi khoản thu trong địa bàn.',0,CONVERT(nvarchar(36),NEWID())),
      (N'r-account',N'ACCOUNT',N'ACCOUNT',N'Tài khoản cơ bản',N'Thông báo và phiên của mình, không có quyền cư dân.',1,CONVERT(nvarchar(36),NEWID()));


    INSERT dbo.ResidentialUnits(Code,Name,UnitType,ParentUnitId,Address) VALUES
      ('KDC-01',N'Khu dân cư Minh Hòa','AREA',NULL,N'Đường Nguyễn Văn Linh, khu dân cư Minh Hòa'),
      ('KDC-02',N'Khu dân cư An Phú','AREA',NULL,N'Đường Lê Lợi, khu dân cư An Phú');
    INSERT dbo.ResidentialUnits(Code,Name,UnitType,ParentUnitId) VALUES
      ('TO-A',N'Tổ dân phố số 1','GROUP',1),('TO-B',N'Tổ dân phố số 2','GROUP',1),('TO-C',N'Tổ dân phố số 3','GROUP',2);
    DECLARE @Catalog TABLE(Code varchar(100), Name nvarchar(200), Module varchar(50), Kind varchar(10));
    INSERT @Catalog VALUES
      ('accounts.read',N'Xem tài khoản','ACCOUNTS','ADMIN'),
      ('accounts.create',N'Tạo tài khoản','ACCOUNTS','ADMIN'),
      ('accounts.update',N'Cập nhật tài khoản','ACCOUNTS','ADMIN'),
      ('accounts.lock',N'Khóa tài khoản','ACCOUNTS','ADMIN'),
      ('accounts.reset_password',N'Hỗ trợ khôi phục mật khẩu','ACCOUNTS','ADMIN'),
      ('roles.read',N'Xem vai trò','AUTHORIZATION','ADMIN'),
      ('roles.create',N'Tạo vai trò','AUTHORIZATION','ADMIN'),
      ('roles.update',N'Cập nhật vai trò','AUTHORIZATION','ADMIN'),
      ('roles.delete',N'Vô hiệu/xóa vai trò chưa sử dụng','AUTHORIZATION','ADMIN'),
      ('roles.assign',N'Gán/thu hồi vai trò theo phạm vi','AUTHORIZATION','ADMIN'),
      ('permissions.read',N'Xem danh mục quyền','AUTHORIZATION','ADMIN'),
      ('permissions.create',N'Tạo Permission','AUTHORIZATION','ADMIN'),
      ('permissions.update',N'Cập nhật Permission','AUTHORIZATION','ADMIN'),
      ('permissions.delete',N'Xóa Permission chưa sử dụng','AUTHORIZATION','ADMIN'),
      ('roles.permissions.update',N'Cấu hình quyền của vai trò','AUTHORIZATION','ADMIN'),
      ('areas.read',N'Xem khu dân cư','AREAS','ADMIN'),
      ('areas.manage',N'Quản lý khu dân cư','AREAS','ADMIN'),
      ('groups.read',N'Xem tổ dân phố','AREAS','ADMIN'),
      ('groups.manage',N'Quản lý tổ dân phố','AREAS','ADMIN'),
      ('audit.read',N'Xem nhật ký metadata','AUDIT','ADMIN'),
      ('reports.aggregate.read',N'Xem báo cáo tổng hợp','REPORTS','ADMIN'),
      ('households.read',N'Xem hộ gia đình','HOUSEHOLDS','OFFICER'),
      ('households.create',N'Thêm hộ gia đình','HOUSEHOLDS','OFFICER'),
      ('households.update',N'Cập nhật hộ gia đình','HOUSEHOLDS','OFFICER'),
      ('households.archive',N'Lưu trữ hộ gia đình','HOUSEHOLDS','OFFICER'),
      ('residents.read',N'Xem cư dân','RESIDENTS','OFFICER'),
      ('residents.create',N'Thêm cư dân','RESIDENTS','OFFICER'),
      ('residents.update',N'Cập nhật thông tin cư dân','RESIDENTS','OFFICER'),
      ('residents.sensitive.read',N'Xem đầy đủ CCCD','RESIDENTS','OFFICER'),
      ('residence_requests.read',N'Xem yêu cầu cư trú','RESIDENCE','OFFICER'),
      ('residence_requests.process',N'Tiếp nhận yêu cầu','RESIDENCE','OFFICER'),
      ('residence_requests.approve',N'Duyệt yêu cầu','RESIDENCE','OFFICER'),
      ('residence_requests.reject',N'Từ chối yêu cầu','RESIDENCE','OFFICER'),
      ('residents.verify',N'Xác minh liên kết tài khoản cư dân','RESIDENTS','OFFICER'),
      ('population_changes.read',N'Xem biến động dân cư','RESIDENCE','OFFICER'),
      ('population_changes.record',N'Ghi nhận biến động dân cư','RESIDENCE','OFFICER'),
      ('fees.read',N'Xem khoản phải thu','FEES','OFFICER'),
      ('fees.manage',N'Quản lý đợt thu','FEES','OFFICER'),
      ('payments.read',N'Xem thanh toán','FEES','OFFICER'),
      ('payments.record',N'Ghi nhận thanh toán','FEES','OFFICER'),
      ('announcements.read',N'Xem thông báo quản trị','ANNOUNCEMENTS','OFFICER'),
      ('announcements.create',N'Soạn thông báo','ANNOUNCEMENTS','OFFICER'),
      ('announcements.update',N'Sửa thông báo','ANNOUNCEMENTS','OFFICER'),
      ('announcements.publish',N'Đăng thông báo','ANNOUNCEMENTS','OFFICER'),
      ('announcements.approve',N'Duyệt hoặc từ chối thông báo','ANNOUNCEMENTS','OFFICER'),
      ('announcements.unpublish',N'Gỡ thông báo','ANNOUNCEMENTS','OFFICER'),
      ('feedback.read',N'Xem phản ánh được giao','FEEDBACK','OFFICER'),
      ('feedback.process',N'Tiếp nhận phản ánh','FEEDBACK','OFFICER'),
      ('feedback.reply',N'Trả lời phản ánh','FEEDBACK','OFFICER'),
      ('feedback.transfer',N'Chuyển phản ánh','FEEDBACK','OFFICER'),
      ('attachments.read',N'Tải giấy tờ của hồ sơ được phép đọc','ATTACHMENTS','OFFICER'),
      ('reports.group.read',N'Xem báo cáo địa bàn','REPORTS','OFFICER'),
      ('reports.export',N'Xuất báo cáo địa bàn','REPORTS','OFFICER'),
      ('profile.self.read',N'Xem thông tin cá nhân','SELF','RESIDENT'),
      ('profile.self.update_contact',N'Cập nhật liên hệ cá nhân','SELF','RESIDENT'),
      ('households.self.read',N'Xem thông tin hộ hiện tại','SELF','RESIDENT'),
      ('residence_requests.self.create',N'Gửi yêu cầu của mình','SELF','RESIDENT'),
      ('residence_requests.self.read',N'Xem yêu cầu của mình','SELF','RESIDENT'),
      ('residence_requests.self.cancel',N'Hủy yêu cầu đang chờ của mình','SELF','RESIDENT'),
      ('feedback.self.create',N'Gửi phản ánh cá nhân','SELF','RESIDENT'),
      ('feedback.self.read',N'Xem phản ánh của mình','SELF','RESIDENT'),
      ('feedback.self.reply',N'Trao đổi trên phản ánh của mình','SELF','RESIDENT'),
      ('fees.self.read',N'Xem khoản của hộ được phép','SELF','RESIDENT'),
      ('payments.self.read',N'Xem kết quả đóng tiền của hộ','SELF','RESIDENT'),
      ('announcements.audience.read',N'Xem thông báo cư dân đúng địa bàn','SELF','RESIDENT'),
      ('attachments.self.upload',N'Gửi giấy tờ cho yêu cầu của mình','SELF','RESIDENT'),
      ('attachments.self.read',N'Tải giấy tờ của mình','SELF','RESIDENT'),
      ('notifications.self.read',N'Xem thông báo cá nhân','SELF','ACCOUNT'),
      ('notifications.self.mark_read',N'Đánh dấu thông báo đã đọc','SELF','ACCOUNT'),
      ('sessions.self.read',N'Xem phiên của mình','SELF','ACCOUNT'),
      ('sessions.self.revoke',N'Thu hồi phiên của mình','SELF','ACCOUNT');
    INSERT dbo.Permissions(Code,Name,Module) SELECT Code,Name,Module FROM @Catalog;
    INSERT dbo.PermissionAllowedScopes(PermissionId,ScopeType)
      SELECT p.Id,CASE c.Kind WHEN 'ADMIN' THEN 'SYSTEM' WHEN 'OFFICER' THEN 'GROUP' ELSE 'SELF' END
      FROM dbo.Permissions p JOIN @Catalog c ON c.Code=p.Code;
    INSERT dbo.PermissionAllowedScopes(PermissionId,ScopeType)
      SELECT p.Id,'AREA' FROM dbo.Permissions p JOIN @Catalog c ON c.Code=p.Code WHERE c.Kind='OFFICER';
    INSERT dbo.RolePermissions(RoleId,PermissionId)
      SELECT CASE c.Kind WHEN 'ADMIN' THEN N'r-admin' WHEN 'OFFICER' THEN N'r-officer' WHEN 'RESIDENT' THEN N'r-resident' ELSE N'r-account' END,p.Id
      FROM dbo.Permissions p JOIN @Catalog c ON p.Code=c.Code;
    INSERT dbo.RolePermissions(RoleId,PermissionId)
      SELECT N'r-collector',Id FROM dbo.Permissions WHERE Code IN ('households.read','residents.read','fees.read','payments.read','payments.record','reports.group.read');

    INSERT dbo.UserRoleAssignments(UserId,RoleId,ScopeType,ResidentialGroupId,ValidFrom,AssignedByUserId) VALUES
      (N'u-admin',N'r-admin','SYSTEM',NULL,DATEADD(day,-30,@Now),N'u-admin'),
      (N'u-officer-a',N'r-officer','GROUP',3,DATEADD(day,-30,@Now),N'u-admin'),
      (N'u-officer-b',N'r-officer','GROUP',4,DATEADD(day,-30,@Now),N'u-admin'),
      (N'u-collector',N'r-collector','GROUP',3,DATEADD(day,-30,@Now),N'u-admin'),
      (N'u-resident-a',N'r-resident','SELF',NULL,DATEADD(day,-30,@Now),N'u-admin'),
      (N'u-resident-b',N'r-resident','SELF',NULL,DATEADD(day,-30,@Now),N'u-admin'),
      (N'u-resident-c',N'r-resident','SELF',NULL,DATEADD(day,-30,@Now),N'u-admin');
    INSERT dbo.UserRoleAssignments(UserId,RoleId,ScopeType,ValidFrom,AssignedByUserId)
      SELECT Id,N'r-account','SELF',DATEADD(day,-30,@Now),N'u-admin' FROM dbo.AspNetUsers;
    -- Expired assignment: canbo01 must NOT get current access to group B.
    INSERT dbo.UserRoleAssignments(UserId,RoleId,ScopeType,ResidentialGroupId,ValidFrom,ValidUntil,AssignedByUserId)
      VALUES(N'u-officer-a',N'r-officer','GROUP',4,DATEADD(day,-90,@Now),DATEADD(day,-31,@Now),N'u-admin');


    INSERT dbo.Households(Code,ResidentialGroupId,Address,RegisteredAt,CreatedByUserId) VALUES
      ('HO-2026-001',3,N'Số 12, đường Hoa Mai, tổ dân phố số 1','2026-01-01',N'u-officer-a'),
      ('HO-2026-002',3,N'Số 24, đường Hoa Mai, tổ dân phố số 1','2026-02-01',N'u-officer-a'),
      ('HO-2026-003',4,N'Số 8, đường Hoa Phượng, tổ dân phố số 2','2026-03-01',N'u-officer-b');
    INSERT dbo.Residents(FullName,CitizenId,DateOfBirth,Gender,Occupation,HouseholdId,RelationshipToHead,ResidenceStatus,MovedInAt,MovedOutAt) VALUES
      (N'Nguyễn Văn An','000000000001','1985-05-15','MALE',N'Nhân viên',1,N'Chủ hộ','RESIDING','2026-01-01',NULL),
      (N'Trần Thị Bình','000000000002','1988-08-20','FEMALE',N'Giáo viên',1,N'Vợ','RESIDING','2026-01-01',NULL),
      (N'Lê Văn Cường','000000000003','1993-03-10','MALE',N'Kỹ thuật viên',3,N'Chủ hộ','TEMPORARY','2026-03-01',NULL),
      (N'Phạm Văn Dũng','000000000004','1980-06-12','MALE',N'Kinh doanh',2,N'Chủ hộ','RESIDING','2026-02-01',NULL),
      (N'Nguyễn Minh',NULL,'2015-09-01','OTHER',N'Học sinh',1,N'Con','RESIDING','2026-01-01',NULL),
      (N'Vũ Thị Hoa','000000000006','1990-11-11','FEMALE',N'Nhân viên',2,N'Vợ','ABSENT','2026-02-01',NULL),
      (N'Đặng Văn Nam','000000000007','1992-12-01','MALE',N'Lao động',NULL,NULL,'MOVED_OUT','2026-02-01','2026-08-01');
    UPDATE dbo.Households SET HeadResidentId=CASE Id WHEN 1 THEN 1 WHEN 2 THEN 4 ELSE 3 END;
    UPDATE dbo.AspNetUsers SET ResidentId=CASE Id WHEN N'u-resident-a' THEN 1 WHEN N'u-resident-b' THEN 2 ELSE 3 END,
      ReviewedByUserId=CASE Id WHEN N'u-resident-c' THEN N'u-officer-b' ELSE N'u-officer-a' END,ReviewedAt=DATEADD(day,-30,@Now)
      WHERE Id IN (N'u-resident-a',N'u-resident-b',N'u-resident-c');

    INSERT dbo.Requests(Code,RequestKind,SubmittedByUserId,ResidentId,AssignedGroupId,RequestType,Subject,Body,
      StartDate,EndDate,TargetHouseholdId,Status,ProcessedByUserId,ProcessedAt,ProcessingNote,ClientRequestKey,SubmittedAt) VALUES
      ('YC-2026-001','RESIDENCE',N'u-resident-a',1,3,'TEMPORARY_ABSENCE',N'Xin tạm vắng',N'Đi công tác tại Đà Nẵng trong một tháng theo thời gian đăng ký.',CONVERT(date,DATEADD(day,7,@Now)),CONVERT(date,DATEADD(day,37,@Now)),1,'PENDING',NULL,NULL,NULL,'seed-yc-001',DATEADD(day,-1,@Now)),
      ('YC-2026-002','RESIDENCE',N'u-resident-b',2,3,'TEMPORARY_ABSENCE',N'Yêu cầu đang xử lý',N'Về quê thăm người thân trong một tuần, thông tin liên hệ đã được cập nhật.',CONVERT(date,DATEADD(day,3,@Now)),CONVERT(date,DATEADD(day,10,@Now)),1,'PROCESSING',NULL,NULL,NULL,'seed-yc-002',DATEADD(day,-2,@Now)),
      ('YC-2026-004','RESIDENCE',N'u-resident-a',1,3,'MOVE_OUT',N'Xin chuyển đi',N'Thiếu thông tin nơi chuyển đến.',CONVERT(date,@Now),NULL,NULL,'REJECTED',N'u-officer-a',@Now,N'Chưa có thông tin nơi chuyển đến.','seed-yc-004',DATEADD(day,-3,@Now));
    INSERT dbo.Requests(Code,RequestKind,SubmittedByUserId,ResidentId,AssignedGroupId,RequestType,Subject,Body,StartDate,EndDate,
      OriginalResidenceStatus,ResultResidenceStatus,TargetHouseholdId,ResultRelationship,ChangeAppliedAt,Status,ProcessedByUserId,ProcessedAt,ClientRequestKey,SubmittedAt) VALUES
      ('YC-2026-003','RESIDENCE',N'u-resident-c',3,4,'TEMPORARY_RESIDENCE',N'Tạm trú đã duyệt',N'Đăng ký tạm trú tại hộ gia đình ở số 8, đường Hoa Phượng để thuận tiện cho công việc.','2026-03-01','2027-03-01',
       'MOVED_OUT','TEMPORARY',3,N'Chủ hộ','2026-03-01','APPROVED',N'u-officer-b','2026-03-01','seed-yc-003','2026-02-25');
    INSERT dbo.Requests(Code,RequestKind,Source,SubmittedByUserId,ResidentId,AssignedGroupId,RequestType,Subject,Body,StartDate,EndDate,
      OriginalHouseholdId,TargetHouseholdId,OriginalResidenceStatus,ResultResidenceStatus,OriginalRelationship,ResultRelationship,ChangeAppliedAt,Status,ProcessedByUserId,ProcessedAt,ClientRequestKey,SubmittedAt) VALUES
      ('BD-2026-001','RESIDENCE','STAFF',N'u-officer-a',6,3,'TEMPORARY_ABSENCE',N'Ghi nhận tạm vắng',N'Cán bộ ghi biến động trực tiếp theo quyền.','2026-09-01','2027-01-01',
       2,2,'RESIDING','ABSENT',N'Vợ',N'Vợ','2026-09-01','APPROVED',N'u-officer-a','2026-09-01','seed-bd-001','2026-09-01'),
      ('BD-2026-002','RESIDENCE','STAFF',N'u-officer-a',7,3,'MOVE_OUT',N'Ghi nhận chuyển đi',N'Cập nhật chuyển nơi cư trú sau khi đối chiếu thông tin tiếp nhận.','2026-08-01',NULL,
       2,NULL,'RESIDING','MOVED_OUT',N'Người thân',NULL,'2026-08-01','APPROVED',N'u-officer-a','2026-08-01','seed-bd-002','2026-08-01');
    INSERT dbo.Requests(Code,RequestKind,SubmittedByUserId,ResidentId,AssignedGroupId,RequestType,Category,Subject,Body,Status,ProcessedByUserId,ProcessedAt,ClientRequestKey) VALUES
      ('PA-2026-001','FEEDBACK',N'u-resident-a',1,3,'FEEDBACK','ENVIRONMENT',N'Góp ý vệ sinh',N'Rác sinh hoạt tại đầu ngõ 12 chưa được thu gom trong hai ngày. Đề nghị bố trí thu gom và thông báo lịch xe rác.','PENDING',NULL,NULL,'seed-pa-001'),
      ('PA-2026-002','FEEDBACK',N'u-resident-b',2,3,'FEEDBACK','INFRASTRUCTURE',N'Đèn chiếu sáng',N'Hai bóng đèn trước số nhà 24 đường Hoa Mai không sáng vào buổi tối. Đề nghị kiểm tra và thay thế.','RESPONDED',N'u-officer-a',@Now,'seed-pa-002'),
      ('PA-2026-003','FEEDBACK',N'u-resident-c',3,4,'FEEDBACK','OTHER',N'Góp ý tổ B',N'Đề nghị sửa khung và bổ sung vị trí niêm yết thông báo tại nhà văn hóa.','PROCESSING',NULL,NULL,'seed-pa-003');
    INSERT dbo.Activities(ActivityType,RequestId,ActorUserId,EventCode,ToStatus,OccurredAt)
      SELECT 'REQUEST_EVENT',Id,SubmittedByUserId,'SUBMITTED','PENDING',SubmittedAt FROM dbo.Requests WHERE Source='RESIDENT';
    INSERT dbo.Activities(ActivityType,RequestId,ActorUserId,EventCode,ToStatus,OccurredAt)
      SELECT 'REQUEST_EVENT',Id,ProcessedByUserId,'STAFF_CHANGE_RECORDED','APPROVED',ProcessedAt FROM dbo.Requests WHERE Source='STAFF';
    INSERT dbo.Activities(ActivityType,RequestId,ActorUserId,EventCode,FromStatus,ToStatus,OccurredAt)
      SELECT 'REQUEST_EVENT',Id,COALESCE(ProcessedByUserId,CASE AssignedGroupId WHEN 3 THEN N'u-officer-a' ELSE N'u-officer-b' END),
        Status,'PENDING',Status,COALESCE(ProcessedAt,SubmittedAt) FROM dbo.Requests WHERE Source='RESIDENT' AND Status<>'PENDING';
    INSERT dbo.Activities(ActivityType,RequestId,ActorUserId,EventCode,Body,ClientMessageKey)
      SELECT 'MESSAGE',Id,N'u-officer-a','OFFICIAL_REPLY',N'Ban điều hành đã tiếp nhận và bố trí kiểm tra hệ thống chiếu sáng trong tuần này.','seed-msg-001' FROM dbo.Requests WHERE Code='PA-2026-002';
    INSERT dbo.Activities(ActivityType,RequestId,ActorUserId,EventCode,Body,ClientMessageKey)
      SELECT 'MESSAGE',Id,N'u-resident-b','RESIDENT_REPLY',N'Cảm ơn, tôi sẽ theo dõi kết quả.','seed-msg-002' FROM dbo.Requests WHERE Code='PA-2026-002';

    INSERT dbo.FeeCampaigns(Code,ResidentialGroupId,Name,Description,FeeType,DueDate,AmountPerHousehold,CreatedByUserId) VALUES
      ('THU-2026-A01',3,N'Vệ sinh môi trường',N'Kinh phí thu gom và vận chuyển rác sinh hoạt tháng 11.','REQUIRED',CONVERT(date,DATEADD(day,30,@Now)),100000,N'u-officer-a'),
      ('THU-2026-A02',3,N'Đóng góp cộng đồng',N'Tự nguyện theo hộ; chỉ lập khoản khi hộ đăng ký đóng góp.','VOLUNTARY',NULL,50000,N'u-officer-a'),
      ('THU-2026-B01',4,N'Vệ sinh tổ B',N'Kinh phí vệ sinh và chăm sóc cảnh quan khu dân cư tháng 11.','REQUIRED',CONVERT(date,DATEADD(day,30,@Now)),120000,N'u-officer-b');
    INSERT dbo.Payments(CampaignId,HouseholdId,AmountDue,IsPaid,PaidAmount,ReceiptCode,Method,PaidAt,RecordedByUserId,ClientPaymentKey) VALUES
      (1,1,100000,1,100000,'PT-2026-001','CASH',@Now,N'u-collector','seed-payment-001'),
      (1,2,100000,0,NULL,NULL,NULL,NULL,NULL,NULL),
      (2,1,50000,0,NULL,NULL,NULL,NULL,NULL,NULL),
      (3,3,120000,0,NULL,NULL,NULL,NULL,NULL,NULL);
    INSERT dbo.Announcements(ResidentialGroupId,Title,Body,Audience,Status,CreatedByUserId,PublishedByUserId,PublishedAt) VALUES
      (3,N'Lịch vệ sinh tổ A',N'Các hộ dân tập trung tại nhà văn hóa lúc 07 giờ Chủ nhật để vệ sinh đường làng và khơi thông rãnh thoát nước.','PUBLIC','PUBLISHED',N'u-officer-a',N'u-officer-a',@Now),
      (3,N'Lịch họp cư dân tổ A',N'Chỉ cư dân tổ A đã xác minh được đọc.','RESIDENTS','PUBLISHED',N'u-officer-a',N'u-officer-a',@Now),
      (4,N'Hoạt động tổ B',N'Thông báo công khai tổ B.','PUBLIC','PUBLISHED',N'u-officer-b',N'u-officer-b',@Now),
      (3,N'Nháp chưa công bố',N'Không xuất hiện ở trang công khai.','PUBLIC','DRAFT',N'u-officer-a',NULL,NULL);
    UPDATE dbo.Announcements SET ReviewStatus='APPROVED', SubmittedAt=PublishedAt, ReviewedAt=PublishedAt, ReviewedByUserId=PublishedByUserId WHERE Status='PUBLISHED';
    INSERT dbo.Activities(ActivityType,RecipientUserId,EventCode,TargetType,TargetId)
      SELECT 'NOTIFICATION',N'u-officer-a','RESIDENCE_REQUEST_CREATED','Request',CONVERT(varchar(64),Id) FROM dbo.Requests WHERE Code='YC-2026-001';
    INSERT dbo.Activities(ActivityType,RecipientUserId,EventCode,TargetType,TargetId)
      SELECT 'NOTIFICATION',N'u-officer-a','FEEDBACK_CREATED','Request',CONVERT(varchar(64),Id) FROM dbo.Requests WHERE Code='PA-2026-001';
    INSERT dbo.Activities(ActivityType,RecipientUserId,EventCode,TargetType,TargetId)
      SELECT 'NOTIFICATION',N'u-resident-b','FEEDBACK_REPLIED','Request',CONVERT(varchar(64),Id) FROM dbo.Requests WHERE Code='PA-2026-002';
    INSERT dbo.Activities(ActivityType,RecipientUserId,EventCode,TargetType,TargetId) VALUES
      ('NOTIFICATION',N'u-resident-a','PAYMENT_RECORDED','Payment','1'),
      ('NOTIFICATION',N'u-resident-c','ANNOUNCEMENT_PUBLISHED','Announcement','3');
    INSERT dbo.Activities(ActivityType,ActorUserId,EventCode,TargetType,TargetId,SafeMetadata) VALUES
      ('AUDIT',N'u-admin','DATABASE_INITIALIZED','Database','QuanLyCuDan',N'{"demo":true,"schemaVersion":"COMPACT-2.0"}');

    -- Scope remains attached to its original assignment, never combined across roles.
    EXEC(N'CREATE OR ALTER VIEW dbo.vw_EffectivePermissions AS
      SELECT a.Id AS AssignmentId,a.UserId,a.RoleId,r.Name AS RoleName,p.Code AS PermissionCode,
        a.ScopeType,a.ResidentialAreaId,a.ResidentialGroupId,a.ValidFrom,a.ValidUntil
      FROM dbo.UserRoleAssignments a
      JOIN dbo.AspNetUsers u ON u.Id=a.UserId AND u.IsActive=1
      JOIN dbo.AspNetRoles r ON r.Id=a.RoleId AND r.IsActive=1
      JOIN dbo.RolePermissions rp ON rp.RoleId=a.RoleId JOIN dbo.Permissions p ON p.Id=rp.PermissionId
      JOIN dbo.PermissionAllowedScopes s ON s.PermissionId=p.Id AND s.ScopeType=a.ScopeType
      WHERE a.RevokedAt IS NULL AND a.ValidFrom<=SYSUTCDATETIME() AND (a.ValidUntil IS NULL OR a.ValidUntil>SYSUTCDATETIME())
        AND (u.LockoutEnd IS NULL OR u.LockoutEnd<=SYSDATETIMEOFFSET())
        AND (a.ScopeType<>''SELF'' OR p.Code LIKE ''notifications.self.%'' OR p.Code LIKE ''sessions.self.%''
             OR u.VerificationStatus=''APPROVED'' AND u.ResidentId IS NOT NULL)
        AND (a.ScopeType<>''AREA'' OR EXISTS(SELECT 1 FROM dbo.ResidentialUnits ar WHERE ar.Id=a.ResidentialAreaId AND ar.UnitType=''AREA'' AND ar.IsActive=1))
        AND (a.ScopeType<>''GROUP'' OR EXISTS(SELECT 1 FROM dbo.ResidentialUnits g JOIN dbo.ResidentialUnits ar ON ar.Id=g.ParentUnitId
             WHERE g.Id=a.ResidentialGroupId AND g.UnitType=''GROUP'' AND g.IsActive=1 AND ar.UnitType=''AREA'' AND ar.IsActive=1));');
    EXEC(N'CREATE OR ALTER VIEW dbo.vw_PopulationChanges AS
      SELECT Id AS RequestId,Code,ResidentId,AssignedGroupId,RequestType AS ChangeType,
        OriginalHouseholdId,TargetHouseholdId,OriginalResidenceStatus,ResultResidenceStatus,
        OriginalRelationship,ResultRelationship,OldHeadResidentId,NewHeadResidentId,
        StartDate AS EffectiveDate,EndDate,ChangeAppliedAt,ProcessedByUserId,ProcessedAt
      FROM dbo.Requests WHERE RequestKind=''RESIDENCE'' AND Status=''APPROVED'';');
    EXEC(N'CREATE OR ALTER VIEW dbo.vw_FeeBalances AS
      SELECT Id AS PaymentId,CampaignId,HouseholdId,AmountDue,IsPaid,IsCancelled,
        CONVERT(decimal(18,2),COALESCE(PaidAmount,0)) AS AmountPaid,
        CONVERT(decimal(18,2),AmountDue-COALESCE(PaidAmount,0)) AS RemainingAmount
      FROM dbo.Payments;');

    IF (SELECT COUNT(*) FROM dbo.AspNetUsers)<>8 THROW 51010, 'Unexpected account count.',1;
    IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>23 THROW 51011, 'Expected 23 total tables (8 business + 14 auth + 1 version).',1;
    IF EXISTS(SELECT 1 FROM dbo.AspNetUserRoles) THROW 51012, 'AspNetUserRoles must stay empty.',1;
    IF EXISTS(SELECT 1 FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.Id=rp.PermissionId WHERE rp.RoleId=N'r-admin' AND p.Code='feedback.read')
      THROW 51013, 'Admin must not get implicit private feedback access.',1;
    IF EXISTS(SELECT 1 FROM dbo.UserRoleAssignments a JOIN dbo.RolePermissions rp ON rp.RoleId=a.RoleId
       WHERE NOT EXISTS(SELECT 1 FROM dbo.PermissionAllowedScopes s WHERE s.PermissionId=rp.PermissionId AND s.ScopeType=a.ScopeType))
      THROW 51014, 'Assignment scope incompatible with permission.',1;
    IF EXISTS(SELECT 1 FROM dbo.ResidentialUnits g JOIN dbo.ResidentialUnits ar ON ar.Id=g.ParentUnitId WHERE g.UnitType='GROUP' AND ar.UnitType<>'AREA')
      THROW 51015, 'Group must be a child of an area.',1;
    IF EXISTS(SELECT 1 FROM dbo.Payments p JOIN dbo.Households h ON h.Id=p.HouseholdId JOIN dbo.FeeCampaigns c ON c.Id=p.CampaignId
       WHERE h.ResidentialGroupId<>c.ResidentialGroupId)
      THROW 51016, 'Cross-group household payment detected.',1;
    IF EXISTS(SELECT 1 FROM dbo.Requests WHERE Source='RESIDENT' AND Status='APPROVED' AND SubmittedByUserId=ProcessedByUserId)
      THROW 51017, 'Resident request cannot be self-approved.',1;
    INSERT dbo.SchemaVersions(VersionCode) VALUES('COMPACT-2.0');
    COMMIT TRANSACTION;
    PRINT N'Created QuanLyCuDan COMPACT-2.0: 8 business tables + 14 auth + 1 version.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
-- Summaries exclude password hashes/tokens/private request bodies.
SELECT UserName,FullName,VerificationStatus FROM dbo.AspNetUsers ORDER BY UserName;
SELECT UnitType,Code,Name,ParentUnitId FROM dbo.ResidentialUnits ORDER BY Id;
SELECT RequestKind,Status,COUNT(*) AS Total FROM dbo.Requests GROUP BY RequestKind,Status;
EXEC(N'SELECT * FROM dbo.vw_FeeBalances;');
GO
-- Additive upgrades run in separate batches even when the install batch returns.
-- Keep these blocks in sync with the standalone scripts in database/.
-- Source: database/20261109_RegistrationProfile.sql
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

-- Source: database/20261109_AnnouncementReview.sql
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

/* APPLICATION CONTRACT / DELIBERATE MVP LIMITS:
  - 8 business tables does not mean 8 total tables. Auth/permission/session tables
    remain separate; SchemaVersions is one installation metadata table.
  - Validate AREA/GROUP types and current active scope for every assignment/resource.
    FKs guarantee existence, while services validate the referenced unit TYPE.
  - Household head changes: clear old head pointer, update resident household and
    head pointers in one transaction; FK guarantees head belongs to the household.
  - Approved change snapshots and non-notification Activities are append-only.
    No UPDATE/DELETE endpoint for approved requests or historical activity rows.
    All direct staff residence edits create an approved Source=STAFF Request;
    ordinary resident requests still require an independent authorized approver.
  - No HouseholdMemberships table: only current household + approved change history,
    not a complete legal membership registry with all historical membership periods.
  - Activities must be filtered by TYPE before any list/read/update. notifications
    query ActivityType=NOTIFICATION AND RecipientUserId=currentUser; audit query
    ActivityType=AUDIT with audit.read; messages/events authorize via parent Request.
    Never expose a generic unrestricted GET /activities endpoint or private Body
    through audit/notification metadata. Read/dispatch updates affect notifications only.
  - RequestKind determines the permission namespace (residence_requests vs feedback).
    Shared storage MUST NOT let residence-only staff read/handle private feedback.
  - Check old state under lock when approving, update Residents/Households + Request
    snapshots + Activities in one transaction with RowVersion. New head must belong
    to the target household. ChangeType/ResultStatus must match the action.
  - One optional attachment per Request, maximum 5 MB PDF/JPEG/PNG, private storage,
    checked file content and authorized parent download. No public physical path.
  - Payments are pre-assessed household dues plus one full paid result. No partial
    payments/refunds/personal fees. Snapshot AmountDue when assigning households;
    do not recompute old dues from an edited campaign. Voluntary dues require opt-in.
    Record payment using WHERE IsPaid=0 AND IsCancelled=0 and RowVersion; log audit.
    No endpoint to reset a paid row or change its money/household/campaign/receipt.
  - Self fee visibility: only current head of the assessed household. Other household
    members cannot view billing detail by guessing IDs. Current user identity,
    linked ResidentId and household head must come from server-owned records.
  - ClientRequestKey and ClientMessageKey provide create idempotency; same key with
    different payload => 409, not blind return. Payments use unique ClientPaymentKey.
  - Business permissions/catalog are code-seeded; API prevents self-escalation,
    preserving the last active admin, and incompatible/overlapping role assignments.
  - Announcements keep DRAFT/PUBLISHED/UNPUBLISHED plus separate review state.
    Submit before approval; publish only approved content. Editing clears approval.
  - JWT/session revocation, rotation, reset one-time tokens and CSRF are app rules.
*/

GO
/* Village portal upgrade. Run after QuanLyCuDan.sql, in the target database.
   Preserves accounts, passwords, business data and assignment history.
   One non-revoked role slot per account (including scheduled assignments).
   Expired slots are closed; conflicting business roles fail for manual resolution.
   No demo identities are inserted by this migration. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID('dbo.SchemaVersions') IS NULL THROW 51100,'Install the base schema first.',1;
GO
BEGIN TRY
 BEGIN TRAN;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource='VillagePortal.3',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51101,'Cannot acquire migration lock.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='VILLAGE-3.0')
 BEGIN COMMIT; RETURN; END;

 UPDATE dbo.UserRoleAssignments SET RevokedAt=SYSUTCDATETIME(),RevocationReason=N'Hết hiệu lực trước chuyển đổi VILLAGE-3.0'
 WHERE RevokedAt IS NULL AND ValidUntil<=SYSUTCDATETIME();
 -- Base ACCOUNT is absorbed by the retained role; RESIDENT is absorbed by a business role.
 UPDATE a SET RevokedAt=SYSUTCDATETIME(),RevocationReason=N'Gộp vai trò nền vào một chức danh VILLAGE-3.0'
 FROM dbo.UserRoleAssignments a JOIN dbo.AspNetRoles r ON r.Id=a.RoleId
 WHERE a.RevokedAt IS NULL AND (r.Name='ACCOUNT' AND EXISTS(SELECT 1 FROM dbo.UserRoleAssignments b JOIN dbo.AspNetRoles br ON br.Id=b.RoleId WHERE b.UserId=a.UserId AND b.RevokedAt IS NULL AND br.Name<>'ACCOUNT')
 OR r.Name='RESIDENT' AND EXISTS(SELECT 1 FROM dbo.UserRoleAssignments b JOIN dbo.AspNetRoles br ON br.Id=b.RoleId WHERE b.UserId=a.UserId AND b.RevokedAt IS NULL AND br.Name NOT IN('ACCOUNT','RESIDENT')));
 IF EXISTS(SELECT UserId FROM dbo.UserRoleAssignments WHERE RevokedAt IS NULL GROUP BY UserId HAVING COUNT(*)>1)
 BEGIN
   SELECT UserId,RoleId,ScopeType,ResidentialAreaId,ResidentialGroupId FROM dbo.UserRoleAssignments
   WHERE RevokedAt IS NULL AND UserId IN(SELECT UserId FROM dbo.UserRoleAssignments WHERE RevokedAt IS NULL GROUP BY UserId HAVING COUNT(*)>1);
   THROW 51102,'Conflicting business role assignments. Revoke extra assignments explicitly, then rerun. No changes committed.',1;
 END;
 CREATE UNIQUE INDEX UX_Assignments_OneRole ON dbo.UserRoleAssignments(UserId) WHERE RevokedAt IS NULL;

 CREATE TABLE dbo.Organizations(
   Id bigint IDENTITY PRIMARY KEY,Code varchar(50) NOT NULL UNIQUE,Name nvarchar(200) NOT NULL,
   ResidentialGroupId bigint NOT NULL REFERENCES dbo.ResidentialUnits(Id),
   IsActive bit NOT NULL DEFAULT 1,RowVersion rowversion NOT NULL,
   CONSTRAINT UQ_Organizations_Group UNIQUE(Id,ResidentialGroupId));
 CREATE TABLE dbo.OrganizationMembers(
   OrganizationId bigint NOT NULL REFERENCES dbo.Organizations(Id),
   ResidentId bigint NOT NULL REFERENCES dbo.Residents(Id),JoinedAt datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
   LeftAt datetime2(3) NULL,RowVersion rowversion NOT NULL,
   PRIMARY KEY(OrganizationId,ResidentId),CHECK(LeftAt IS NULL OR LeftAt>=JoinedAt));
 ALTER TABLE dbo.UserRoleAssignments ADD OrganizationId bigint NULL REFERENCES dbo.Organizations(Id);
 EXEC(N'ALTER TABLE dbo.UserRoleAssignments ADD CONSTRAINT CK_Assignments_Organization CHECK(OrganizationId IS NULL OR ScopeType=''GROUP'');
 ALTER TABLE dbo.UserRoleAssignments ADD CONSTRAINT FK_Assignments_OrganizationGroup
   FOREIGN KEY(OrganizationId,ResidentialGroupId) REFERENCES dbo.Organizations(Id,ResidentialGroupId);');
 -- GROUP remains the village boundary; optional OrganizationId narrows it.
 ALTER TABLE dbo.Announcements ADD OrganizationId bigint NULL;
 EXEC(N'ALTER TABLE dbo.Announcements ADD CONSTRAINT FK_Announcements_OrganizationGroup
   FOREIGN KEY(OrganizationId,ResidentialGroupId) REFERENCES dbo.Organizations(Id,ResidentialGroupId);');
 DECLARE @checks nvarchar(max)=N'';
 SELECT @checks=@checks+N'ALTER TABLE dbo.Announcements DROP CONSTRAINT '+QUOTENAME(name)+N';'
 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.Announcements') AND definition LIKE '%Audience%';
 EXEC sys.sp_executesql @checks;
 ALTER TABLE dbo.Announcements ADD CONSTRAINT CK_Announcements_Audience CHECK(Audience IN('PUBLIC','RESIDENTS','ORGANIZATION'));
 EXEC(N'ALTER TABLE dbo.Announcements ADD CONSTRAINT CK_Announcements_Organization CHECK(
   Audience=''ORGANIZATION'' AND OrganizationId IS NOT NULL OR Audience<>''ORGANIZATION'' AND OrganizationId IS NULL);');
 EXEC(N'CREATE INDEX IX_Announcements_Organization ON dbo.Announcements(OrganizationId,Status,PublishedAt);');
 CREATE UNIQUE INDEX UX_Activities_AnnouncementRecipient ON dbo.Activities(RecipientUserId,TargetId)
 WHERE ActivityType='NOTIFICATION' AND TargetType='Announcement' AND EventCode='ANNOUNCEMENT_PUBLISHED';

 DECLARE @roles TABLE(Id nvarchar(450),Name nvarchar(256),DisplayName nvarchar(200));
 INSERT @roles VALUES
 ('r-village-head','VILLAGE_HEAD',N'Trưởng thôn'),('r-party-secretary','PARTY_SECRETARY',N'Bí thư chi bộ'),
 ('r-org-head','ORGANIZATION_HEAD',N'Trưởng tổ chức'),('r-deputy','DEPUTY',N'Cấp phó'),('r-commune','COMMUNE_OFFICER',N'Cán bộ xã');
 INSERT dbo.AspNetRoles(Id,Name,NormalizedName,DisplayName,IsSystemRole,ConcurrencyStamp)
 SELECT Id,Name,Name,DisplayName,1,CONVERT(nvarchar(36),NEWID()) FROM @roles s
 WHERE NOT EXISTS(SELECT 1 FROM dbo.AspNetRoles r WHERE r.NormalizedName=s.Name);
 DECLARE @permissions TABLE(Code varchar(100),Name nvarchar(200),Module varchar(50),ScopeType varchar(10));
 INSERT @permissions VALUES
 ('organizations.read',N'Xem tổ chức','ORGANIZATIONS','GROUP'),
 ('organizations.manage',N'Quản lý tổ chức','ORGANIZATIONS','SYSTEM'),
 ('organizations.members.manage',N'Quản lý thành viên tổ chức','ORGANIZATIONS','GROUP'),
 ('announcements.statistics',N'Xem thống kê đã đọc','ANNOUNCEMENTS','GROUP');
 INSERT dbo.Permissions(Code,Name,Module) SELECT Code,Name,Module FROM @permissions s WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p WHERE p.Code=s.Code);
 INSERT dbo.PermissionAllowedScopes(PermissionId,ScopeType) SELECT p.Id,s.ScopeType FROM @permissions s JOIN dbo.Permissions p ON p.Code=s.Code;
 INSERT dbo.PermissionAllowedScopes(PermissionId,ScopeType)
 SELECT Id,'SYSTEM' FROM dbo.Permissions WHERE Code='organizations.read';
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
 WHERE r.Name='ADMIN' AND p.Code IN('organizations.read','organizations.manage')
 AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
 -- Every retained role carries its own personal session/notification permissions.
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
 WHERE (p.Code LIKE 'notifications.self.%' OR p.Code LIKE 'sessions.self.%')
 AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
 WHERE r.Name IN('RESIDENT','VILLAGE_HEAD','PARTY_SECRETARY','ORGANIZATION_HEAD','DEPUTY','COMMUNE_OFFICER','OFFICER','FEE_COLLECTOR')
 AND p.Module='SELF' AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
 WHERE r.Name IN('VILLAGE_HEAD','PARTY_SECRETARY','ORGANIZATION_HEAD','DEPUTY')
 AND p.Code IN('announcements.read','announcements.create','announcements.update','announcements.statistics','organizations.read')
 AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
 WHERE r.Name IN('VILLAGE_HEAD','PARTY_SECRETARY') AND p.Code IN('announcements.approve','announcements.unpublish','feedback.read','feedback.process','feedback.reply')
 AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p
 WHERE r.Name='ORGANIZATION_HEAD' AND p.Code='organizations.members.manage';
 -- Retire manual publish permission and remove approval/removal from non-leaders.
 DELETE rp FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.Id=rp.PermissionId JOIN dbo.AspNetRoles r ON r.Id=rp.RoleId
 WHERE p.Code='announcements.publish' OR p.Code IN('announcements.approve','announcements.unpublish') AND r.Name NOT IN('VILLAGE_HEAD','PARTY_SECRETARY');
 UPDATE dbo.AspNetUsers SET PermissionVersion=PermissionVersion+1;
 INSERT dbo.Activities(ActivityType,EventCode,TargetType,TargetId,SafeMetadata)
 VALUES('AUDIT','VILLAGE_SCHEMA_MIGRATED','Database',DB_NAME(),N'{"version":"VILLAGE-3.0","singleRole":true}');
 EXEC(N'CREATE OR ALTER VIEW dbo.vw_EffectivePermissions AS
 SELECT a.Id AssignmentId,a.UserId,a.RoleId,r.Name RoleName,p.Code PermissionCode,
   s.ScopeType,a.ResidentialAreaId,a.ResidentialGroupId,a.OrganizationId,a.ValidFrom,a.ValidUntil
 FROM dbo.UserRoleAssignments a
 JOIN dbo.AspNetUsers u ON u.Id=a.UserId AND u.IsActive=1
 JOIN dbo.AspNetRoles r ON r.Id=a.RoleId AND r.IsActive=1
 JOIN dbo.RolePermissions rp ON rp.RoleId=r.Id JOIN dbo.Permissions p ON p.Id=rp.PermissionId
 JOIN dbo.PermissionAllowedScopes s ON s.PermissionId=p.Id AND (s.ScopeType=''SELF'' OR s.ScopeType=a.ScopeType)
 LEFT JOIN dbo.Organizations o ON o.Id=a.OrganizationId
 WHERE a.RevokedAt IS NULL AND a.ValidFrom<=SYSUTCDATETIME() AND (a.ValidUntil IS NULL OR a.ValidUntil>SYSUTCDATETIME())
 AND (u.LockoutEnd IS NULL OR u.LockoutEnd<=SYSDATETIMEOFFSET())
 AND u.VerificationStatus NOT IN(''PENDING'',''REJECTED'')
 AND (s.ScopeType<>''SELF'' OR p.Code LIKE ''notifications.self.%'' OR p.Code LIKE ''sessions.self.%'' OR u.VerificationStatus=''APPROVED'' AND u.ResidentId IS NOT NULL)
 AND (a.ScopeType<>''AREA'' OR EXISTS(SELECT 1 FROM dbo.ResidentialUnits ar WHERE ar.Id=a.ResidentialAreaId AND ar.UnitType=''AREA'' AND ar.IsActive=1))
 AND (a.ScopeType<>''GROUP'' OR EXISTS(SELECT 1 FROM dbo.ResidentialUnits g JOIN dbo.ResidentialUnits ar ON ar.Id=g.ParentUnitId WHERE g.Id=a.ResidentialGroupId AND g.UnitType=''GROUP'' AND g.IsActive=1 AND ar.UnitType=''AREA'' AND ar.IsActive=1))
 AND (a.OrganizationId IS NULL OR o.IsActive=1 AND o.ResidentialGroupId=a.ResidentialGroupId)
 AND (p.Code NOT IN(''announcements.approve'',''announcements.unpublish'') OR r.Name IN(''VILLAGE_HEAD'',''PARTY_SECRETARY'') AND a.OrganizationId IS NULL)
 AND p.Code<>''announcements.publish'';');
 INSERT dbo.SchemaVersions(VersionCode) VALUES('VILLAGE-3.0');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO
-- SELF rights are projected from the SAME role; linked-resident checks still apply.
BEGIN TRAN;
DELETE rp FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.Id=rp.PermissionId JOIN dbo.AspNetRoles r ON r.Id=rp.RoleId
WHERE p.Code='announcements.publish' OR p.Code IN('announcements.approve','announcements.unpublish') AND r.Name NOT IN('VILLAGE_HEAD','PARTY_SECRETARY');
EXEC(N'CREATE OR ALTER VIEW dbo.vw_EffectivePermissions AS
 SELECT a.Id AssignmentId,a.UserId,a.RoleId,r.Name RoleName,p.Code PermissionCode,
   s.ScopeType,a.ResidentialAreaId,a.ResidentialGroupId,a.OrganizationId,a.ValidFrom,a.ValidUntil
 FROM dbo.UserRoleAssignments a
 JOIN dbo.AspNetUsers u ON u.Id=a.UserId AND u.IsActive=1
 JOIN dbo.AspNetRoles r ON r.Id=a.RoleId AND r.IsActive=1
 JOIN dbo.RolePermissions rp ON rp.RoleId=r.Id JOIN dbo.Permissions p ON p.Id=rp.PermissionId
 JOIN dbo.PermissionAllowedScopes s ON s.PermissionId=p.Id AND (s.ScopeType=''SELF'' OR s.ScopeType=a.ScopeType)
 LEFT JOIN dbo.Organizations o ON o.Id=a.OrganizationId
 WHERE a.RevokedAt IS NULL AND a.ValidFrom<=SYSUTCDATETIME() AND (a.ValidUntil IS NULL OR a.ValidUntil>SYSUTCDATETIME())
 AND (u.LockoutEnd IS NULL OR u.LockoutEnd<=SYSDATETIMEOFFSET())
 AND u.VerificationStatus NOT IN(''PENDING'',''REJECTED'')
 AND (s.ScopeType<>''SELF'' OR p.Code LIKE ''notifications.self.%'' OR p.Code LIKE ''sessions.self.%'' OR u.VerificationStatus=''APPROVED'' AND u.ResidentId IS NOT NULL)
 AND (a.ScopeType<>''AREA'' OR EXISTS(SELECT 1 FROM dbo.ResidentialUnits ar WHERE ar.Id=a.ResidentialAreaId AND ar.UnitType=''AREA'' AND ar.IsActive=1))
 AND (a.ScopeType<>''GROUP'' OR EXISTS(SELECT 1 FROM dbo.ResidentialUnits g JOIN dbo.ResidentialUnits ar ON ar.Id=g.ParentUnitId WHERE g.Id=a.ResidentialGroupId AND g.UnitType=''GROUP'' AND g.IsActive=1 AND ar.UnitType=''AREA'' AND ar.IsActive=1))
 AND (a.OrganizationId IS NULL OR o.IsActive=1 AND o.ResidentialGroupId=a.ResidentialGroupId)
 AND (p.Code NOT IN(''announcements.approve'',''announcements.unpublish'') OR r.Name IN(''VILLAGE_HEAD'',''PARTY_SECRETARY'') AND a.OrganizationId IS NULL)
 AND p.Code<>''announcements.publish'';');
COMMIT;
GO

GO
/* Directive metadata and portable private PDF storage. No filesystem dependency. */
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRAN;
 IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='VILLAGE-3.1') BEGIN COMMIT; RETURN; END;
 ALTER TABLE dbo.Announcements ADD
   Kind varchar(20) NOT NULL CONSTRAINT DF_Announcements_Kind DEFAULT 'ANNOUNCEMENT',
   DueAt datetime2(3) NULL,DocumentFileName nvarchar(255) NULL,DocumentContent varbinary(max) NULL;
 EXEC(N'ALTER TABLE dbo.Announcements ADD CONSTRAINT CK_Announcements_Kind CHECK(Kind IN(''ANNOUNCEMENT'',''DIRECTIVE'')),
 CONSTRAINT CK_Announcements_Document CHECK(DocumentContent IS NULL AND DocumentFileName IS NULL OR DocumentContent IS NOT NULL AND DocumentFileName IS NOT NULL AND DATALENGTH(DocumentContent) BETWEEN 1 AND 5242880);');
 INSERT dbo.PermissionAllowedScopes(PermissionId,ScopeType)
 SELECT Id,'AREA' FROM dbo.Permissions p WHERE p.Code IN('announcements.statistics','organizations.read')
 AND NOT EXISTS(SELECT 1 FROM dbo.PermissionAllowedScopes s WHERE s.PermissionId=p.Id AND s.ScopeType='AREA');
 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT r.Id,p.Id FROM dbo.AspNetRoles r CROSS JOIN dbo.Permissions p WHERE r.Name='COMMUNE_OFFICER'
 AND p.Code IN('announcements.read','announcements.create','announcements.update','announcements.statistics')
 AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
 INSERT dbo.SchemaVersions(VersionCode) VALUES('VILLAGE-3.1');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO

GO
/* Fictional village demonstration data. No real personal information.
   Additive, rerunnable; never resets an existing account's password.
   New demo accounts share password: 12345678 */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRAN;
 IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='VILLAGE-DEMO-3.0') BEGIN COMMIT; RETURN; END;
 DECLARE @area bigint,@group bigint,@other bigint,@now datetime2(3)=SYSUTCDATETIME();
 IF NOT EXISTS(SELECT 1 FROM dbo.ResidentialUnits WHERE Code='XA-MINH-AN')
 INSERT dbo.ResidentialUnits(Code,Name,UnitType,Address) VALUES('XA-MINH-AN',N'Xã Minh An','AREA',N'Trung tâm hành chính xã Minh An');
 SELECT @area=Id FROM dbo.ResidentialUnits WHERE Code='XA-MINH-AN';
 IF NOT EXISTS(SELECT 1 FROM dbo.ResidentialUnits WHERE Code='THON-AN-HOA')
 INSERT dbo.ResidentialUnits(Code,Name,UnitType,ParentUnitId) VALUES('THON-AN-HOA',N'Thôn An Hòa','GROUP',@area);
 IF NOT EXISTS(SELECT 1 FROM dbo.ResidentialUnits WHERE Code='THON-BINH-MINH')
 INSERT dbo.ResidentialUnits(Code,Name,UnitType,ParentUnitId) VALUES('THON-BINH-MINH',N'Thôn Bình Minh','GROUP',@area);
 SELECT @group=Id FROM dbo.ResidentialUnits WHERE Code='THON-AN-HOA';
 SELECT @other=Id FROM dbo.ResidentialUnits WHERE Code='THON-BINH-MINH';
 INSERT dbo.Organizations(Code,Name,ResidentialGroupId) VALUES
 ('AH-PHU-NU',N'Chi hội Phụ nữ thôn An Hòa',@group),('AH-CHI-DOAN',N'Chi đoàn thôn An Hòa',@group),
 ('AH-CHI-BO',N'Chi bộ thôn An Hòa',@group),('BM-PHU-NU',N'Chi hội Phụ nữ thôn Bình Minh',@other);
 DECLARE @women bigint=(SELECT Id FROM dbo.Organizations WHERE Code='AH-PHU-NU');
 DECLARE @youth bigint=(SELECT Id FROM dbo.Organizations WHERE Code='AH-CHI-DOAN');
 DECLARE @party bigint=(SELECT Id FROM dbo.Organizations WHERE Code='AH-CHI-BO');
 DECLARE @users TABLE(Id nvarchar(450),UserName nvarchar(256),FullName nvarchar(200),RoleName nvarchar(256),OrganizationId bigint,GroupId bigint);
 INSERT @users VALUES
 ('demo-head','truongthon',N'Nguyễn Văn Hòa','VILLAGE_HEAD',NULL,@group),
 ('demo-secretary','bithu',N'Trần Thị Lan','PARTY_SECRETARY',NULL,@group),
 ('demo-org-head','hoiphunu',N'Lê Thị Mai','ORGANIZATION_HEAD',@women,@group),
 ('demo-youth-head','chidoan',N'Phạm Minh Tuấn','ORGANIZATION_HEAD',@youth,@group),
 ('demo-deputy','cap pho',N'Đặng Thị Hạnh','DEPUTY',@women,@group),
 ('demo-deputy-village','phothon',N'Võ Văn Bình','DEPUTY',NULL,@group),
 ('demo-commune','canboxa',N'Bùi Thanh Sơn','COMMUNE_OFFICER',NULL,NULL),
 ('demo-resident','cudan',N'Nguyễn Thị Thu','RESIDENT',NULL,@group),
 ('demo-outsider','cudan_khac',N'Hoàng Văn Nam','RESIDENT',NULL,@other);
 UPDATE @users SET UserName='cappho' WHERE Id='demo-deputy';
 IF EXISTS(SELECT 1 FROM @users s JOIN dbo.AspNetUsers u ON u.Id=s.Id OR u.NormalizedUserName=UPPER(s.UserName))
 THROW 51200,'Demo usernames or IDs already exist. Choose another seed namespace; no existing accounts were changed.',1;
 DECLARE @id nvarchar(450),@username nvarchar(256),@name nvarchar(200),@salt varbinary(16),@resident bigint,@house bigint,@gid bigint;
 DECLARE users CURSOR LOCAL FAST_FORWARD FOR SELECT Id,UserName,FullName,GroupId FROM @users;
 OPEN users; FETCH NEXT FROM users INTO @id,@username,@name,@gid;
 WHILE @@FETCH_STATUS=0
 BEGIN
   SET @salt=CRYPT_GEN_RANDOM(16);
   SET @resident=NULL;
   IF @gid IS NOT NULL
   BEGIN
     INSERT dbo.Households(Code,ResidentialGroupId,Address,RegisteredAt,CreatedByUserId) VALUES('HO-'+@username,@gid,N'Số '+CONVERT(nvarchar(10),ABS(CHECKSUM(@id))%90+1)+N', đường Hoa Sen',CONVERT(date,@now),'u-admin');
     SET @house=SCOPE_IDENTITY();
     INSERT dbo.Residents(FullName,Gender,HouseholdId,RelationshipToHead,ResidenceStatus,DateOfBirth)
     VALUES(@name,'OTHER',@house,N'Chủ hộ','RESIDING','1990-06-15');
     SET @resident=SCOPE_IDENTITY();
     UPDATE dbo.Households SET HeadResidentId=@resident WHERE Id=@house;
   END;
   INSERT dbo.AspNetUsers(Id,UserName,NormalizedUserName,FullName,ResidentId,VerificationStatus,PasswordHash,SecurityStamp,ConcurrencyStamp)
   VALUES(@id,@username,UPPER(@username),@name,@resident,CASE WHEN @resident IS NULL THEN 'NOT_APPLICABLE' ELSE 'APPROVED' END,
     N'SHA256$'+CONVERT(varchar(32),@salt,2)+N'$'+CONVERT(varchar(64),HASHBYTES('SHA2_256',@salt+CONVERT(varbinary(max),N'12345678')),2),CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()));
   FETCH NEXT FROM users INTO @id,@username,@name,@gid;
 END;
 CLOSE users; DEALLOCATE users;
 INSERT dbo.UserRoleAssignments(UserId,RoleId,ScopeType,ResidentialGroupId,ResidentialAreaId,OrganizationId,ValidFrom,AssignedByUserId)
 SELECT s.Id,r.Id,CASE WHEN s.RoleName='RESIDENT' THEN 'SELF' WHEN s.RoleName='COMMUNE_OFFICER' THEN 'AREA' ELSE 'GROUP' END,
 CASE WHEN s.RoleName NOT IN('RESIDENT','COMMUNE_OFFICER') THEN s.GroupId END,
 CASE WHEN s.RoleName='COMMUNE_OFFICER' THEN @area END,s.OrganizationId,DATEADD(day,-10,@now),'u-admin'
 FROM @users s JOIN dbo.AspNetRoles r ON r.Name=s.RoleName;
 INSERT dbo.OrganizationMembers(OrganizationId,ResidentId,JoinedAt)
 SELECT @women,ResidentId,DATEADD(day,-30,@now) FROM dbo.AspNetUsers WHERE Id IN('demo-org-head','demo-deputy','demo-resident');
 INSERT dbo.OrganizationMembers(OrganizationId,ResidentId,JoinedAt)
 SELECT @youth,ResidentId,DATEADD(day,-30,@now) FROM dbo.AspNetUsers WHERE Id='demo-youth-head';
 INSERT dbo.OrganizationMembers(OrganizationId,ResidentId,JoinedAt)
 SELECT @party,ResidentId,DATEADD(day,-30,@now) FROM dbo.AspNetUsers WHERE Id IN('demo-head','demo-secretary');
 INSERT dbo.Announcements(ResidentialGroupId,Title,Body,Audience,Status,ReviewStatus,CreatedByUserId,SubmittedAt)
 VALUES(@group,N'Lịch tổng vệ sinh đường làng cuối tuần',N'Kính mời các hộ dân tham gia tổng vệ sinh vào 07 giờ Chủ nhật. Tập trung tại nhà văn hóa thôn; mỗi hộ chuẩn bị chổi và dụng cụ phù hợp.','RESIDENTS','DRAFT','PENDING','demo-deputy-village',@now);
 INSERT dbo.Announcements(ResidentialGroupId,OrganizationId,Title,Body,Audience,Status,ReviewStatus,CreatedByUserId,SubmittedAt)
 VALUES(@group,@women,N'Sinh hoạt Chi hội Phụ nữ tháng 11',N'Trao đổi kế hoạch hỗ trợ hội viên và phân công chuẩn bị ngày hội tại nhà văn hóa. Nội dung chỉ dành cho thành viên chi hội.','ORGANIZATION','DRAFT','PENDING','demo-org-head',@now),
 (@group,@youth,N'Kế hoạch hỗ trợ kỹ năng số cho người dân',N'Dự thảo lịch hướng dẫn sử dụng dịch vụ công trực tuyến; chờ cấp trưởng thôn hoặc Bí thư xét duyệt.','ORGANIZATION','DRAFT','DRAFT','demo-youth-head',NULL);
 INSERT dbo.Announcements(ResidentialGroupId,Title,Body,Audience,Status,ReviewStatus,CreatedByUserId,SubmittedAt,ReviewedByUserId,ReviewedAt,PublishedByUserId,PublishedAt)
 VALUES(@group,N'Thông báo lịch tiếp nhận phản ánh tại nhà văn hóa',N'Ban điều hành thôn tiếp nhận ý kiến người dân vào chiều thứ Sáu hằng tuần. Vui lòng ghi rõ nội dung và địa điểm cần xử lý.','PUBLIC','PUBLISHED','APPROVED','demo-head',@now,'demo-head',@now,'demo-head',@now);
 INSERT dbo.Activities(ActivityType,RecipientUserId,EventCode,TargetType,TargetId,OccurredAt)
 SELECT 'NOTIFICATION',u.Id,'ANNOUNCEMENT_PUBLISHED','Announcement',CONVERT(varchar(20),a.Id),@now
 FROM dbo.Announcements a CROSS JOIN dbo.AspNetUsers u JOIN dbo.Residents r ON r.Id=u.ResidentId JOIN dbo.Households h ON h.Id=r.HouseholdId
 WHERE a.CreatedByUserId='demo-head' AND a.Status='PUBLISHED' AND h.ResidentialGroupId=@group;
 INSERT dbo.SchemaVersions(VersionCode) VALUES('VILLAGE-DEMO-3.0');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO

GO
/* Presentation refresh for the known seeded records only.
   Names/content are synthetic; presentation uses ordinary operational wording.
   No external personal information is imported. Other application data is preserved.
   Resets the 17 known sample accounts to the requested password 12345678 ONCE.
   Run in the target database after VILLAGE-3.1 and village seed. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource='VillagePortal.Data.3.2',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51400,'Cannot acquire data migration lock.',1;
 IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='VILLAGE-DATA-3.2') BEGIN COMMIT; RETURN; END;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE VersionCode='VILLAGE-3.1') THROW 51401,'Install VILLAGE-3.1 before refreshing seed data.',1;

 DECLARE @names TABLE(Id nvarchar(450),FullName nvarchar(150));
 INSERT @names VALUES
 ('u-admin',N'Nguyễn Thành Trung'),('u-officer-a',N'Đặng Văn Hải'),('u-officer-b',N'Trần Quốc Hùng'),
 ('u-collector',N'Phạm Thị Hồng'),('u-resident-a',N'Nguyễn Văn An'),('u-resident-b',N'Trần Thị Bình'),
 ('u-resident-c',N'Lê Văn Cường'),('u-pending',N'Đỗ Thị Hương'),
 ('demo-head',N'Nguyễn Văn Hòa'),('demo-secretary',N'Trần Thị Lan'),('demo-org-head',N'Lê Thị Mai'),
 ('demo-youth-head',N'Phạm Minh Tuấn'),('demo-deputy',N'Đặng Thị Hạnh'),('demo-deputy-village',N'Võ Văn Bình'),
 ('demo-commune',N'Bùi Thanh Sơn'),('demo-resident',N'Nguyễn Thị Thu'),('demo-outsider',N'Hoàng Văn Nam');
 UPDATE u SET FullName=n.FullName FROM dbo.AspNetUsers u JOIN @names n ON n.Id=u.Id;
 UPDATE r SET FullName=n.FullName FROM dbo.Residents r JOIN dbo.AspNetUsers u ON u.ResidentId=r.Id JOIN @names n ON n.Id=u.Id;
 UPDATE dbo.Residents SET FullName=REPLACE(FullName,N' Demo',N''),Occupation=REPLACE(Occupation,N' Demo',N'')
 WHERE FullName IN(N'Phạm Văn Dũng Demo',N'Nguyễn Minh Demo',N'Vũ Thị Hoa Demo',N'Đặng Văn Nam Demo');
 UPDATE r SET Occupation=REPLACE(r.Occupation,N' Demo',N'') FROM dbo.Residents r JOIN dbo.AspNetUsers u ON u.ResidentId=r.Id JOIN @names n ON n.Id=u.Id;

 DECLARE @units TABLE(OldCode varchar(30),NewCode varchar(30),Name nvarchar(200),Address nvarchar(300));
 INSERT @units VALUES
 ('KDC-DEMO-01','KDC-01',N'Khu dân cư Minh Hòa',N'Đường Nguyễn Văn Linh, khu dân cư Minh Hòa'),
 ('KDC-DEMO-02','KDC-02',N'Khu dân cư An Phú',N'Đường Lê Lợi, khu dân cư An Phú'),
 ('TO-A','TO-A',N'Tổ dân phố số 1',N'Khu dân cư Minh Hòa'),('TO-B','TO-B',N'Tổ dân phố số 2',N'Khu dân cư Minh Hòa'),
 ('TO-C','TO-C',N'Tổ dân phố số 3',N'Khu dân cư An Phú'),
 ('XA-MINH-AN-DEMO','XA-MINH-AN',N'Xã Minh An',N'Trung tâm hành chính xã Minh An'),
 ('THON-AN-HOA-DEMO','THON-AN-HOA',N'Thôn An Hòa',N'Xã Minh An'),
 ('THON-BINH-MINH-DEMO','THON-BINH-MINH',N'Thôn Bình Minh',N'Xã Minh An');
 UPDATE u SET Code=n.NewCode,Name=n.Name,Address=n.Address FROM dbo.ResidentialUnits u JOIN @units n ON u.Code IN(n.OldCode,n.NewCode);
 UPDATE dbo.Organizations SET Code=REPLACE(Code,'-DEMO','') WHERE Code IN('AH-PHU-NU-DEMO','AH-CHI-DOAN-DEMO','AH-CHI-BO-DEMO','BM-PHU-NU-DEMO');
 UPDATE dbo.Households SET Address=CASE Code
 WHEN 'HO-DEMO-001' THEN N'Số 12, đường Hoa Mai, tổ dân phố số 1'
 WHEN 'HO-DEMO-002' THEN N'Số 24, đường Hoa Mai, tổ dân phố số 1'
 ELSE N'Số 8, đường Hoa Phượng, tổ dân phố số 2' END,
 Code=REPLACE(Code,'HO-DEMO-','HO-2026-') WHERE Code IN('HO-DEMO-001','HO-DEMO-002','HO-DEMO-003');
 UPDATE dbo.Households SET Address=REPLACE(Address,N' (địa chỉ giả lập)',N''),Code=REPLACE(Code,'DEMO-','HO-')
 WHERE Code IN('DEMO-truongthon','DEMO-bithu','DEMO-hoiphunu','DEMO-chidoan','DEMO-cappho','DEMO-phothon','DEMO-cudan','DEMO-cudan_khac');

 DECLARE @requests TABLE(OldCode varchar(30),NewCode varchar(30),Subject nvarchar(250),Body nvarchar(max));
 INSERT @requests VALUES
 ('YC-DEMO-001','YC-2026-001',N'Đăng ký tạm vắng do đi công tác',N'Tôi đi công tác tại Đà Nẵng trong một tháng. Kính đề nghị ghi nhận thời gian tạm vắng theo ngày đăng ký.'),
 ('YC-DEMO-002','YC-2026-002',N'Đăng ký tạm vắng để thăm người thân',N'Tôi cùng gia đình về quê thăm người thân trong một tuần. Thông tin liên hệ đã được cập nhật trong hồ sơ.'),
 ('YC-DEMO-003','YC-2026-003',N'Đăng ký tạm trú tại tổ dân phố số 2',N'Tôi đăng ký tạm trú tại hộ gia đình ở số 8, đường Hoa Phượng để thuận tiện cho công việc.'),
 ('YC-DEMO-004','YC-2026-004',N'Đề nghị xác nhận chuyển nơi cư trú',N'Tôi dự kiến chuyển nơi cư trú và sẽ bổ sung địa chỉ tiếp nhận theo yêu cầu của cán bộ.'),
 ('BD-DEMO-001','BD-2026-001',N'Ghi nhận tạm vắng của thành viên hộ',N'Ghi nhận thời gian tạm vắng theo thông tin đã xác minh với chủ hộ.'),
 ('BD-DEMO-002','BD-2026-002',N'Ghi nhận chuyển nơi cư trú',N'Cập nhật trạng thái chuyển đi sau khi hoàn tất đối chiếu thông tin cư trú.'),
 ('PA-DEMO-001','PA-2026-001',N'Đề nghị thu gom rác tại ngõ 12',N'Rác sinh hoạt tại đầu ngõ 12 chưa được thu gom trong hai ngày. Đề nghị bố trí thu gom và thông báo lại lịch xe rác.'),
 ('PA-DEMO-002','PA-2026-002',N'Đèn chiếu sáng đường Hoa Mai bị hỏng',N'Hai bóng đèn trước số nhà 24 đường Hoa Mai không sáng vào buổi tối. Đề nghị kiểm tra, thay thế để bảo đảm đi lại.'),
 ('PA-DEMO-003','PA-2026-003',N'Đề nghị bổ sung bảng tin tại nhà văn hóa',N'Bảng tin ngoài nhà văn hóa đã xuống cấp. Đề nghị sửa khung và bổ sung vị trí niêm yết thông báo cho người dân.');
 UPDATE r SET Code=n.NewCode,Subject=n.Subject,Body=n.Body FROM dbo.Requests r JOIN @requests n ON r.Code IN(n.OldCode,n.NewCode);
 UPDATE a SET Body=N'Ban điều hành đã tiếp nhận phản ánh và bố trí kiểm tra hệ thống chiếu sáng trong tuần này.'
 FROM dbo.Activities a JOIN dbo.Requests r ON r.Id=a.RequestId WHERE r.Code='PA-2026-002' AND a.ClientMessageKey='seed-msg-001';
 UPDATE dbo.FeeCampaigns SET Code=REPLACE(Code,'-DEMO-','-2026-'),Name=REPLACE(Name,N' Demo',N''),
 Description=CASE Code WHEN 'THU-DEMO-A01' THEN N'Kinh phí thu gom và vận chuyển rác sinh hoạt tháng 11.'
 WHEN 'THU-DEMO-B01' THEN N'Kinh phí vệ sinh và chăm sóc cảnh quan khu dân cư tháng 11.'
 ELSE N'Đóng góp tự nguyện để sửa chữa trang thiết bị nhà văn hóa.' END
 WHERE Code IN('THU-DEMO-A01','THU-DEMO-A02','THU-DEMO-B01');
 UPDATE dbo.Payments SET ReceiptCode='PT-2026-001' WHERE ReceiptCode='PT-DEMO-001';

 UPDATE dbo.Announcements SET Title=REPLACE(Title,N' Demo',N''),Body=CASE
 WHEN Title IN(N'Lịch vệ sinh tổ A Demo',N'Lịch vệ sinh tổ A') THEN N'Các hộ dân tập trung tại nhà văn hóa lúc 07 giờ Chủ nhật để vệ sinh đường làng và khơi thông rãnh thoát nước. Mỗi hộ chuẩn bị dụng cụ phù hợp.'
 WHEN Title IN(N'Lịch họp cư dân tổ A Demo',N'Lịch họp cư dân tổ A') THEN N'Kính mời đại diện các hộ gia đình dự họp lúc 19 giờ 30 thứ Sáu tại nhà văn hóa. Nội dung: công tác vệ sinh, an ninh và kế hoạch sinh hoạt cộng đồng.'
 WHEN Title IN(N'Hoạt động tổ B Demo',N'Hoạt động tổ B') THEN N'Tổ dân phố tổ chức chăm sóc cây xanh và kiểm tra các điểm đèn chiếu sáng. Các hộ phản ánh vị trí cần xử lý tới ban điều hành.'
 ELSE N'Dự thảo kế hoạch sinh hoạt cộng đồng tháng tới; đang tổng hợp ý kiến trước khi gửi xét duyệt.' END
 WHERE CreatedByUserId IN('u-officer-a','u-officer-b') AND Title IN(N'Lịch vệ sinh tổ A Demo',N'Lịch vệ sinh tổ A',N'Lịch họp cư dân tổ A Demo',N'Lịch họp cư dân tổ A',N'Hoạt động tổ B Demo',N'Hoạt động tổ B',N'Nháp chưa công bố Demo',N'Nháp chưa công bố');
 UPDATE dbo.Announcements SET Body=REPLACE(REPLACE(REPLACE(Body,
 N' Đây là dữ liệu minh họa.',N''),N'; dữ liệu giả lập.',N'.'),N' Thông tin minh họa, không phải lịch tiếp dân thực tế.',N'')
 WHERE CreatedByUserId IN('demo-head','demo-org-head','demo-deputy-village','demo-youth-head');

 -- Random independent salt per account; application upgrades these hashes on login.
 DECLARE @id nvarchar(450),@salt varbinary(16);
 DECLARE passwords CURSOR LOCAL FAST_FORWARD FOR SELECT u.Id FROM dbo.AspNetUsers u JOIN @names n ON n.Id=u.Id;
 OPEN passwords; FETCH NEXT FROM passwords INTO @id;
 WHILE @@FETCH_STATUS=0
 BEGIN
   SET @salt=CRYPT_GEN_RANDOM(16);
   UPDATE dbo.AspNetUsers SET PasswordHash=N'SHA256$'+CONVERT(varchar(32),@salt,2)+N'$'+CONVERT(varchar(64),HASHBYTES('SHA2_256',@salt+CONVERT(varbinary(max),N'12345678')),2),
   SecurityStamp=CONVERT(nvarchar(36),NEWID()),AccessFailedCount=0,LockoutEnd=NULL,PermissionVersion=PermissionVersion+1 WHERE Id=@id;
   FETCH NEXT FROM passwords INTO @id;
 END;
 CLOSE passwords; DEALLOCATE passwords;
 UPDATE s SET RevokedAt=SYSUTCDATETIME(),RevocationReason='SEED_DATA_REFRESH'
 FROM dbo.UserSessions s JOIN @names n ON n.Id=s.UserId WHERE s.RevokedAt IS NULL;
 INSERT dbo.Activities(ActivityType,EventCode,TargetType,TargetId,SafeMetadata)
 VALUES('AUDIT','SEED_DATA_REFRESHED','Database',DB_NAME(),N'{"version":"VILLAGE-DATA-3.2","synthetic":true}');
 INSERT dbo.SchemaVersions(VersionCode) VALUES('VILLAGE-DATA-3.2');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO
