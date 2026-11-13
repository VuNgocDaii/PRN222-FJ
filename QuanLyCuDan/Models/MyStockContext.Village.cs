using Microsoft.EntityFrameworkCore;

namespace QuanLyCuDan.Models;

public partial class Announcement
{
    public string Kind { get; set; } = "ANNOUNCEMENT";
    public DateTime? DueAt { get; set; }
    public string? DocumentFileName { get; set; }
    public byte[]? DocumentContent { get; set; }
    public long? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public partial class UserRoleAssignment
{
    public long? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public partial class VwEffectivePermission
{
    public long? OrganizationId { get; set; }
}

public partial class MyStockContext
{
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.ToTable("Organizations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.ResidentialGroup).WithMany().HasForeignKey(x => x.ResidentialGroupId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<OrganizationMember>(e =>
        {
            e.ToTable("OrganizationMembers");
            e.HasKey(x => new { x.OrganizationId, x.ResidentId });
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.JoinedAt).HasPrecision(3);
            e.Property(x => x.LeftAt).HasPrecision(3);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Resident).WithMany().HasForeignKey(x => x.ResidentId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<Announcement>().Property(x => x.Kind).HasMaxLength(20).IsUnicode(false);
        modelBuilder.Entity<Announcement>().Property(x => x.DueAt).HasPrecision(3);
        modelBuilder.Entity<Announcement>().Property(x => x.DocumentFileName).HasMaxLength(255);
        modelBuilder.Entity<Announcement>().HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<UserRoleAssignment>().HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<UserRoleAssignment>().HasIndex(x => x.UserId).IsUnique().HasFilter("[RevokedAt] IS NULL").HasDatabaseName("UX_Assignments_OneRole");
    }
}
