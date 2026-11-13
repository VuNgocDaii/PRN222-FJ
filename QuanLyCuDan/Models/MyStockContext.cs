using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace QuanLyCuDan.Models;

public partial class MyStockContext : DbContext
{
    public MyStockContext(DbContextOptions<MyStockContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Activity> Activities
    {
        get; set;
    }

    public virtual DbSet<Announcement> Announcements
    {
        get; set;
    }

    public virtual DbSet<AspNetRole> AspNetRoles
    {
        get; set;
    }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims
    {
        get; set;
    }

    public virtual DbSet<AspNetUser> AspNetUsers
    {
        get; set;
    }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims
    {
        get; set;
    }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins
    {
        get; set;
    }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens
    {
        get; set;
    }

    public virtual DbSet<FeeCampaign> FeeCampaigns
    {
        get; set;
    }

    public virtual DbSet<Household> Households
    {
        get; set;
    }

    public virtual DbSet<PasswordResetChallenge> PasswordResetChallenges
    {
        get; set;
    }

    public virtual DbSet<Payment> Payments
    {
        get; set;
    }

    public virtual DbSet<Permission> Permissions
    {
        get; set;
    }

    public virtual DbSet<PermissionAllowedScope> PermissionAllowedScopes
    {
        get; set;
    }

    public virtual DbSet<RefreshToken> RefreshTokens
    {
        get; set;
    }

    public virtual DbSet<Request> Requests
    {
        get; set;
    }

    public virtual DbSet<Resident> Residents
    {
        get; set;
    }

    public virtual DbSet<ResidentialUnit> ResidentialUnits
    {
        get; set;
    }

    public virtual DbSet<SchemaVersion> SchemaVersions
    {
        get; set;
    }

    public virtual DbSet<UserRoleAssignment> UserRoleAssignments
    {
        get; set;
    }

    public virtual DbSet<UserSession> UserSessions
    {
        get; set;
    }

    public virtual DbSet<VwEffectivePermission> VwEffectivePermissions
    {
        get; set;
    }

    public virtual DbSet<VwFeeBalance> VwFeeBalances
    {
        get; set;
    }

    public virtual DbSet<VwPopulationChange> VwPopulationChanges
    {
        get; set;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Activiti__3214EC073E454D68");

            entity.HasIndex(e => new { e.ActivityType, e.DispatchedAt, e.NextAttemptAt }, "IX_Activities_Dispatch");

            entity.HasIndex(e => new { e.RecipientUserId, e.ActivityType, e.ReadAt, e.OccurredAt }, "IX_Activities_Recipient");

            entity.HasIndex(e => new { e.RequestId, e.OccurredAt }, "IX_Activities_Request");

            entity.HasIndex(e => new { e.ActorUserId, e.ClientMessageKey }, "UX_Activities_ClientMessage")
                .IsUnique()
                .HasFilter("([ClientMessageKey] IS NOT NULL)");

            entity.Property(e => e.ActivityType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Body).HasMaxLength(4000);
            entity.Property(e => e.ClientMessageKey)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.DispatchedAt).HasPrecision(3);
            entity.Property(e => e.EventCode)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.FromStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NextAttemptAt).HasPrecision(3);
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ReadAt).HasPrecision(3);
            entity.Property(e => e.SafeMetadata).HasMaxLength(2000);
            entity.Property(e => e.TargetId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.TargetType)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.ToStatus)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.ActorUser).WithMany(p => p.ActivityActorUsers)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK__Activitie__Actor__1332DBDC");

            entity.HasOne(d => d.FromGroup).WithMany(p => p.ActivityFromGroups)
                .HasForeignKey(d => d.FromGroupId)
                .HasConstraintName("FK__Activitie__FromG__151B244E");

            entity.HasOne(d => d.RecipientUser).WithMany(p => p.ActivityRecipientUsers)
                .HasForeignKey(d => d.RecipientUserId)
                .HasConstraintName("FK__Activitie__Recip__14270015");

            entity.HasOne(d => d.Request).WithMany(p => p.Activities)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK__Activitie__Reque__123EB7A3");

            entity.HasOne(d => d.ToGroup).WithMany(p => p.ActivityToGroups)
                .HasForeignKey(d => d.ToGroupId)
                .HasConstraintName("FK__Activitie__ToGro__160F4887");
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.Property(e => e.ReviewStatus).HasMaxLength(15).IsUnicode(false).HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmittedAt).HasPrecision(3);
            entity.Property(e => e.ReviewedAt).HasPrecision(3);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.ReviewNote).HasMaxLength(1000);
            entity.HasOne<AspNetUser>().WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasKey(e => e.Id).HasName("PK__Announce__3214EC07CED635DE");

            entity.HasIndex(e => new { e.Status, e.Audience, e.PublishedAt }, "IX_Announcements_Public");

            entity.Property(e => e.Audience)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.ExpiresAt).HasPrecision(3);
            entity.Property(e => e.PublishedAt).HasPrecision(3);
            entity.Property(e => e.PublishedByUserId).HasMaxLength(450);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.Title).HasMaxLength(250);
            entity.Property(e => e.UnpublishReason).HasMaxLength(1000);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.AnnouncementCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Announcem__Creat__367C1819");

            entity.HasOne(d => d.PublishedByUser).WithMany(p => p.AnnouncementPublishedByUsers)
                .HasForeignKey(d => d.PublishedByUserId)
                .HasConstraintName("FK__Announcem__Publi__37703C52");

            entity.HasOne(d => d.ResidentialGroup).WithMany(p => p.Announcements)
                .HasForeignKey(d => d.ResidentialGroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Announcem__Resid__32AB8735");
        });

        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__RolePermi__Permi__571DF1D5"),
                    l => l.HasOne<AspNetRole>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__RolePermi__RoleI__5629CD9C"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("PK__RolePerm__6400A1A85B3B2BB1");
                        j.ToTable("RolePermissions");
                        j.HasIndex(new[] { "PermissionId" }, "IX_RolePermissions_Permission");
                    });
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AspNetRo__3214EC079FF73C2D");

            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AspNetRol__RoleI__3B75D760");
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.ResidentId, "UX_Users_Resident")
                .IsUnique()
                .HasFilter("([ResidentId] IS NOT NULL)");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LockoutEnabled).HasDefaultValue(true);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.PermissionVersion).HasDefaultValue(1);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.ReviewedAt).HasPrecision(3);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.VerificationStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.Resident).WithOne(p => p.AspNetUser)
                .HasForeignKey<AspNetUser>(d => d.ResidentId)
                .HasConstraintName("FK_Users_Resident");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.InverseReviewedByUser)
                .HasForeignKey(d => d.ReviewedByUserId)
                .HasConstraintName("FK_Users_Reviewer");

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__AspNetUse__RoleI__44FF419A"),
                    l => l.HasOne<AspNetUser>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__AspNetUse__UserI__440B1D61"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId").HasName("PK__AspNetUs__AF2760ADD667E0C4");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AspNetUs__3214EC07CA286B9F");

            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AspNetUse__UserI__38996AB5");
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey }).HasName("PK__AspNetUs__2B2C5B521EF8E31A");

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.ProviderKey).HasMaxLength(128);

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AspNetUse__UserI__3E52440B");
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name }).HasName("PK__AspNetUs__8CC4984123752ACC");

            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(128);

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AspNetUse__UserI__412EB0B6");
        });

        modelBuilder.Entity<FeeCampaign>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FeeCampa__3214EC073F3A94E1");

            entity.HasIndex(e => e.Code, "UQ__FeeCampa__A25C5AA7C8D770CE").IsUnique();

            entity.Property(e => e.AmountPerHousehold).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FeeType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.FeeCampaigns)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FeeCampai__Creat__236943A5");

            entity.HasOne(d => d.ResidentialGroup).WithMany(p => p.FeeCampaigns)
                .HasForeignKey(d => d.ResidentialGroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FeeCampai__Resid__1F98B2C1");
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Househol__3214EC0794251524");

            entity.HasIndex(e => new { e.ResidentialGroupId, e.IsArchived }, "IX_Households_Group");

            entity.HasIndex(e => e.Code, "UQ__Househol__A25C5AA77F961DBC").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Households)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Household__Creat__66603565");

            entity.HasOne(d => d.ResidentialGroup).WithMany(p => p.Households)
                .HasForeignKey(d => d.ResidentialGroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Household__Resid__6477ECF3");

            entity.HasOne(d => d.Resident).WithMany(p => p.Households)
                // EF không hỗ trợ NULL trong alternate key (Id, HouseholdId).
                // Chỉ map navigation theo Id; SQL vẫn giữ FK kép để kiểm tra chủ hộ thuộc hộ.
                .HasForeignKey(d => d.HeadResidentId)
                .HasConstraintName("FK_Households_HeadMember");
        });

        modelBuilder.Entity<PasswordResetChallenge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC078E4B5600");

            entity.HasIndex(e => e.TokenHash, "UQ__Password__BCB33F92A7C282DC").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ConsumedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.ExpiresAt).HasPrecision(3);
            entity.Property(e => e.TokenHash)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.UserId).HasMaxLength(450);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.PasswordResetChallengeCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PasswordR__Creat__4D5F7D71");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetChallengeUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PasswordR__UserI__4C6B5938");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC0751D39C62");

            entity.HasIndex(e => new { e.HouseholdId, e.IsPaid, e.IsCancelled }, "IX_Payments_Household");

            entity.HasIndex(e => new { e.CampaignId, e.HouseholdId }, "UQ__Payments__EE1BB7F496264265").IsUnique();

            entity.HasIndex(e => e.ClientPaymentKey, "UX_Payments_ClientKey")
                .IsUnique()
                .HasFilter("([ClientPaymentKey] IS NOT NULL)");

            entity.HasIndex(e => e.ReceiptCode, "UX_Payments_Receipt")
                .IsUnique()
                .HasFilter("([ReceiptCode] IS NOT NULL)");

            entity.Property(e => e.AmountDue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CancellationReason).HasMaxLength(1000);
            entity.Property(e => e.ClientPaymentKey)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Method)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaidAt).HasPrecision(3);
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.RecordedByUserId).HasMaxLength(450);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Campaign).WithMany(p => p.Payments)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Campai__282DF8C2");

            entity.HasOne(d => d.Household).WithMany(p => p.Payments)
                .HasForeignKey(d => d.HouseholdId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Househ__29221CFB");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.Payments)
                .HasForeignKey(d => d.RecordedByUserId)
                .HasConstraintName("FK__Payments__Record__2BFE89A6");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Permissi__3214EC0714C2B6F6");

            entity.HasIndex(e => e.Code, "UQ__Permissi__A25C5AA79810DB3A").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Module)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<PermissionAllowedScope>(entity =>
        {
            entity.HasKey(e => new { e.PermissionId, e.ScopeType }).HasName("PK__Permissi__CFDB806E25E811C4");

            entity.Property(e => e.ScopeType)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Permission).WithMany(p => p.PermissionAllowedScopes)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Permissio__Permi__52593CB8");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RefreshT__3214EC0721C24E64");

            entity.HasIndex(e => e.SessionId, "IX_RefreshTokens_Session");

            entity.HasIndex(e => e.TokenHash, "UQ__RefreshT__BCB33F92589524C1").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ConsumedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ExpiresAt).HasPrecision(3);
            entity.Property(e => e.RevokedAt).HasPrecision(3);
            entity.Property(e => e.TokenHash)
                .HasMaxLength(32)
                .IsFixedLength();

            entity.HasOne(d => d.ReplacedByToken).WithMany(p => p.InverseReplacedByToken)
                .HasForeignKey(d => d.ReplacedByTokenId)
                .HasConstraintName("FK__RefreshTo__Repla__46B27FE2");

            entity.HasOne(d => d.Session).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RefreshTo__Sessi__44CA3770");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Requests__3214EC073E17248A");

            entity.HasIndex(e => new { e.ResidentId, e.Status, e.ChangeAppliedAt }, "IX_Requests_Changes");

            entity.HasIndex(e => new { e.AssignedGroupId, e.RequestKind, e.Status, e.SubmittedAt }, "IX_Requests_Inbox");

            entity.HasIndex(e => new { e.SubmittedByUserId, e.RequestKind, e.SubmittedAt }, "IX_Requests_Mine");

            entity.HasIndex(e => e.Code, "UQ__Requests__A25C5AA796FF8831").IsUnique();

            entity.HasIndex(e => new { e.SubmittedByUserId, e.ClientRequestKey }, "UQ__Requests__C0BA356E93562DE1").IsUnique();

            entity.HasIndex(e => e.AttachmentStorageKey, "UX_Requests_Attachment")
                .IsUnique()
                .HasFilter("([AttachmentStorageKey] IS NOT NULL)");

            entity.Property(e => e.AttachmentContentType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AttachmentFileName).HasMaxLength(255);
            entity.Property(e => e.AttachmentHash)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.AttachmentStorageKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Body).HasMaxLength(4000);
            entity.Property(e => e.Category)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ChangeAppliedAt).HasPrecision(3);
            entity.Property(e => e.ClientRequestKey)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.OriginalRelationship).HasMaxLength(50);
            entity.Property(e => e.OriginalResidenceStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ProcessedAt).HasPrecision(3);
            entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
            entity.Property(e => e.ProcessingNote).HasMaxLength(2000);
            entity.Property(e => e.RequestKind)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.RequestType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ResultRelationship).HasMaxLength(50);
            entity.Property(e => e.ResultResidenceStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Source)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("RESIDENT");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.Subject).HasMaxLength(250);
            entity.Property(e => e.SubmittedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.AssignedGroup).WithMany(p => p.Requests)
                .HasForeignKey(d => d.AssignedGroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Requests__Assign__7C4F7684");

            entity.HasOne(d => d.NewHeadResident).WithMany(p => p.RequestNewHeadResidents)
                .HasForeignKey(d => d.NewHeadResidentId)
                .HasConstraintName("FK__Requests__NewHea__00200768");

            entity.HasOne(d => d.OldHeadResident).WithMany(p => p.RequestOldHeadResidents)
                .HasForeignKey(d => d.OldHeadResidentId)
                .HasConstraintName("FK__Requests__OldHea__7F2BE32F");

            entity.HasOne(d => d.OriginalHousehold).WithMany(p => p.RequestOriginalHouseholds)
                .HasForeignKey(d => d.OriginalHouseholdId)
                .HasConstraintName("FK__Requests__Origin__7D439ABD");

            entity.HasOne(d => d.ProcessedByUser).WithMany(p => p.RequestProcessedByUsers)
                .HasForeignKey(d => d.ProcessedByUserId)
                .HasConstraintName("FK__Requests__Proces__02084FDA");

            entity.HasOne(d => d.Resident).WithMany(p => p.RequestResidents)
                .HasForeignKey(d => d.ResidentId)
                .HasConstraintName("FK__Requests__Reside__7B5B524B");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.RequestSubmittedByUsers)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Requests__Submit__7A672E12");

            entity.HasOne(d => d.TargetHousehold).WithMany(p => p.RequestTargetHouseholds)
                .HasForeignKey(d => d.TargetHouseholdId)
                .HasConstraintName("FK__Requests__Target__7E37BEF6");
        });

        modelBuilder.Entity<Resident>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Resident__3214EC07BDA324E0");

            entity.HasIndex(e => new { e.HouseholdId, e.ResidenceStatus }, "IX_Residents_Household");

            entity.HasIndex(e => e.FullName, "IX_Residents_Name");

            entity.HasIndex(e => e.PhoneNumber, "IX_Residents_Phone");

            entity.HasIndex(e => new { e.Id, e.HouseholdId }, "UQ_Residents_IdHousehold").IsUnique();

            entity.HasIndex(e => e.CitizenId, "UX_Residents_CitizenId")
                .IsUnique()
                .HasFilter("([CitizenId] IS NOT NULL)");

            entity.Property(e => e.CitizenId)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Gender)
                .HasMaxLength(10)
                .IsUnicode(false);
            // Cư dân chưa được xếp hộ vẫn có thể đăng ký tài khoản.
            entity.Property(e => e.HouseholdId).IsRequired(false);
            entity.Property(e => e.Occupation).HasMaxLength(150);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RelationshipToHead).HasMaxLength(50);
            entity.Property(e => e.ResidenceStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Household).WithMany(p => p.Residents)
                .HasForeignKey(d => d.HouseholdId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Residents__House__6C190EBB");
        });

        modelBuilder.Entity<ResidentialUnit>(entity =>
        {
            entity.HasIndex(e => new { e.ParentUnitId, e.UnitType }, "IX_Units_Parent");

            entity.HasIndex(e => e.Code, "UQ__Resident__A25C5AA7E272D201").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UnitType)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.ParentUnit).WithMany(p => p.InverseParentUnit)
                .HasForeignKey(d => d.ParentUnitId)
                .HasConstraintName("FK__Residenti__Paren__49C3F6B7");
        });

        modelBuilder.Entity<SchemaVersion>(entity =>
        {
            entity.HasKey(e => e.VersionCode).HasName("PK__SchemaVe__8C29168822CED6EA");

            entity.Property(e => e.VersionCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.InstalledAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserRole__3214EC078EC6E462");

            entity.HasIndex(e => new { e.ResidentialGroupId, e.RoleId }, "IX_Assignments_Group");

            entity.HasIndex(e => new { e.UserId, e.RevokedAt, e.ValidFrom, e.ValidUntil }, "IX_Assignments_User");

            entity.Property(e => e.AssignedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.AssignedByUserId).HasMaxLength(450);
            entity.Property(e => e.RevocationReason).HasMaxLength(500);
            entity.Property(e => e.RevokedAt).HasPrecision(3);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ScopeType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ValidFrom).HasPrecision(3);
            entity.Property(e => e.ValidUntil).HasPrecision(3);

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.UserRoleAssignmentAssignedByUsers)
                .HasForeignKey(d => d.AssignedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserRoleA__Assig__5DCAEF64");

            entity.HasOne(d => d.ResidentialArea).WithMany(p => p.UserRoleAssignmentResidentialAreas)
                .HasForeignKey(d => d.ResidentialAreaId)
                .HasConstraintName("FK__UserRoleA__Resid__5BE2A6F2");

            entity.HasOne(d => d.ResidentialGroup).WithMany(p => p.UserRoleAssignmentResidentialGroups)
                .HasForeignKey(d => d.ResidentialGroupId)
                .HasConstraintName("FK__UserRoleA__Resid__5CD6CB2B");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoleAssignments)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserRoleA__RoleI__5AEE82B9");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleAssignmentUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserRoleA__UserI__59FA5E80");
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserSess__3214EC073A461347");

            entity.HasIndex(e => new { e.UserId, e.RevokedAt, e.AbsoluteExpiresAt }, "IX_Sessions_User");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AbsoluteExpiresAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DeviceLabel).HasMaxLength(200);
            entity.Property(e => e.RevocationReason).HasMaxLength(500);
            entity.Property(e => e.RevokedAt).HasPrecision(3);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.User).WithMany(p => p.UserSessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserSessi__UserI__3E1D39E1");
        });

        modelBuilder.Entity<VwEffectivePermission>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_EffectivePermissions");

            entity.Property(e => e.PermissionCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.RoleId).HasMaxLength(450);
            entity.Property(e => e.RoleName).HasMaxLength(256);
            entity.Property(e => e.ScopeType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.ValidFrom).HasPrecision(3);
            entity.Property(e => e.ValidUntil).HasPrecision(3);
        });

        modelBuilder.Entity<VwFeeBalance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_FeeBalances");

            entity.Property(e => e.AmountDue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AmountPaid).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentId).ValueGeneratedOnAdd();
            entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwPopulationChange>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PopulationChanges");

            entity.Property(e => e.ChangeAppliedAt).HasPrecision(3);
            entity.Property(e => e.ChangeType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.OriginalRelationship).HasMaxLength(50);
            entity.Property(e => e.OriginalResidenceStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ProcessedAt).HasPrecision(3);
            entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
            entity.Property(e => e.RequestId).ValueGeneratedOnAdd();
            entity.Property(e => e.ResultRelationship).HasMaxLength(50);
            entity.Property(e => e.ResultResidenceStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
