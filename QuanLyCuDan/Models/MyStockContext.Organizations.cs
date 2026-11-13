using Microsoft.EntityFrameworkCore;

namespace QuanLyCuDan.Models;

// Separate partial mapping keeps database-first mappings and all other modules unchanged.
public partial class MyStockContext
{
    public DbSet<PortalOrganization> PortalOrganizations => Set<PortalOrganization>();
    public DbSet<PortalOrganizationPosition> PortalOrganizationPositions => Set<PortalOrganizationPosition>();
    public DbSet<PortalOrganizationAssignment> PortalOrganizationAssignments => Set<PortalOrganizationAssignment>();
    public DbSet<PortalOrganizationMember> PortalOrganizationMembers => Set<PortalOrganizationMember>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PortalOrganization>(e => {
            e.ToTable("PortalOrganizations"); e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(24).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.SourceNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.ResidentialGroupId, x.Code }).IsUnique();
            e.HasIndex(x => new { x.ResidentialGroupId, x.IsActive, x.IsPublic });
            e.HasOne(x => x.ResidentialGroup).WithMany().HasForeignKey(x => x.ResidentialGroupId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<PortalOrganizationPosition>(e => {
            e.ToTable("PortalOrganizationPositions"); e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SourceText).HasMaxLength(500);
            e.Property(x => x.SourceNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.HasOne(x => x.Organization).WithMany(x => x.Positions).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<PortalOrganizationAssignment>(e => {
            e.ToTable("PortalOrganizationAssignments"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PositionId, x.ResidentId }).IsUnique();
            e.HasIndex(x => new { x.ResidentId, x.IsActive, x.IsConfirmed });
            e.HasOne(x => x.Position).WithMany(x => x.Assignments).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Resident).WithMany().HasForeignKey(x => x.ResidentId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<PortalOrganizationMember>(e => {
            e.ToTable("PortalOrganizationMembers"); e.HasKey(x => x.Id);
            e.Property(x => x.RoleLabel).HasMaxLength(200);
            e.HasIndex(x => new { x.OrganizationId, x.ResidentId }).IsUnique();
            e.HasIndex(x => new { x.ResidentId, x.IsActive, x.IsConfirmed });
            e.HasOne(x => x.Organization).WithMany(x => x.Members).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Resident).WithMany().HasForeignKey(x => x.ResidentId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
