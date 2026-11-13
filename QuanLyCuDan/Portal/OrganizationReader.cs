using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Portal;

// No write endpoints, role grants or management workflows are introduced by this feature.
public sealed class OrganizationReader(MyStockContext db)
{
    public const string SchemaVersion = "PORTAL-ORG-1.0";
    private Task<bool> Available() => db.SchemaVersions.AsNoTracking().AnyAsync(v => v.VersionCode == SchemaVersion);
    private IQueryable<PortalOrganization> Scoped(long groupId, bool publicOnly) => db.PortalOrganizations.AsNoTracking().Where(o =>
        o.ResidentialGroupId == groupId && o.IsActive && (!publicOnly || o.IsPublic) &&
        o.ResidentialGroup.IsActive && o.ResidentialGroup.UnitType == "GROUP" && o.ResidentialGroup.ParentUnit != null &&
        o.ResidentialGroup.ParentUnit.UnitType == "AREA" && o.ResidentialGroup.ParentUnit.IsActive);

    public async Task<OrganizationDirectoryViewModel> Directory(long groupId, bool publicOnly = true, string? kind = null, bool associationsOnly = false)
    {
        if (!await Available()) return new();
        var query = Scoped(groupId, publicOnly);
        if (associationsOnly) query = query.Where(o => o.Kind == "ASSOCIATION" || o.Kind == "SOCIAL");
        if (kind != null) query = query.Where(o => o.Kind == kind);
        return new() { IsAvailable = true, Kind = kind, Items = await query.OrderBy(o => o.Kind == "PARTY" ? 0 :
                o.Kind == "LOCAL_GOVERNMENT" ? 1 : o.Kind == "FRONT" ? 2 : o.Kind == "ASSOCIATION" ? 3 : o.Kind == "SECURITY" ? 4 : 5).ThenBy(o => o.Name)
            .Select(o => new OrganizationCard(o.Id, o.Code, o.Name, o.Kind, o.Description, o.Positions.Count)).ToListAsync() };
    }

    public async Task<OrganizationDetailViewModel?> Detail(long id, long groupId, bool publicOnly = true)
    {
        if (!await Available()) return null;
        var organization = await Scoped(groupId, publicOnly).Include(o => o.Positions).SingleOrDefaultAsync(o => o.Id == id);
        if (organization == null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        // Select names only: private identity fields never enter the public view model.
        var names = await db.PortalOrganizationAssignments.AsNoTracking().Where(a => a.Position.OrganizationId == id &&
            a.IsActive && a.IsConfirmed && a.PublishName && (a.StartsOn == null || a.StartsOn <= today) && (a.EndsOn == null || a.EndsOn >= today) &&
            a.Resident.Household != null && !a.Resident.Household.IsArchived && a.Resident.Household.ResidentialGroupId == groupId)
            .Select(a => new { a.PositionId, a.Resident.FullName }).ToListAsync();
        return new() { Organization = new(organization.Id, organization.Code, organization.Name, organization.Kind, organization.Description, organization.Positions.Count),
            SourceNote = organization.SourceNote, IsResident = !publicOnly,
            Positions = organization.Positions.OrderBy(p => p.Id).Select(p => new OrganizationPositionItem(p.Name, p.MinimumCount, p.MaximumCount,
                p.NeedsClarification, p.SourceNote, names.Where(n => n.PositionId == p.Id).Select(n => n.FullName).ToArray())).ToList() };
    }

    public async Task<MyOrganizationsViewModel> Mine(long residentId, long groupId)
    {
        if (!await Available()) return new();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        // Never accept a resident/user ID from the browser. The caller supplies the linked signed-in resident.
        var organizations = Scoped(groupId, false);
        var members = await db.PortalOrganizationMembers.AsNoTracking().Where(m => m.ResidentId == residentId && m.IsActive && m.IsConfirmed &&
            (m.JoinedOn == null || m.JoinedOn <= today) && (m.LeftOn == null || m.LeftOn >= today) && organizations.Any(o => o.Id == m.OrganizationId))
            .Select(m => new { m.OrganizationId, m.RoleLabel }).ToListAsync();
        var appointments = await db.PortalOrganizationAssignments.AsNoTracking().Where(a => a.ResidentId == residentId && a.IsActive && a.IsConfirmed &&
            (a.StartsOn == null || a.StartsOn <= today) && (a.EndsOn == null || a.EndsOn >= today) && organizations.Any(o => o.Id == a.Position.OrganizationId))
            .Select(a => new { a.Position.OrganizationId, a.Position.Name }).ToListAsync();
        var ids = members.Select(m => m.OrganizationId).Concat(appointments.Select(a => a.OrganizationId)).Distinct().ToArray();
        var mine = await organizations.Where(o => ids.Contains(o.Id)).OrderBy(o => o.Id).Select(o => new { o.Id, o.Name, o.Kind }).ToListAsync();
        return new() { IsAvailable = true, Items = mine.Select(o => new MyOrganizationItem(o.Id, o.Name, o.Kind,
            members.FirstOrDefault(m => m.OrganizationId == o.Id)?.RoleLabel, appointments.Where(a => a.OrganizationId == o.Id).Select(a => a.Name).Distinct().ToArray())).ToList() };
    }
}
