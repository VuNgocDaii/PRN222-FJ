using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class OrganizationRepository(MyStockContext db) : IOrganizationRepository
{
    private IQueryable<Organization> Accessible(bool all, IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds)
    {
        if (all)
            return db.Organizations;
        var groups = groupIds.ToArray();
        var own = organizationIds.ToArray();
        return db.Organizations.Where(o => groups.Contains(o.ResidentialGroupId) || own.Contains(o.Id));
    }

    public Task<List<Organization>> GetAccessibleAsync(bool all, IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds) =>
        Accessible(all, groupIds, organizationIds).AsNoTracking().Include(o => o.ResidentialGroup).OrderBy(o => o.Name).ToListAsync();

    public Task<Organization?> FindActiveAccessibleAsync(bool all, IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds, long id) =>
        Accessible(all, groupIds, organizationIds).SingleOrDefaultAsync(o => o.Id == id && o.IsActive);

    public Task<List<Organization>> GetActiveChoicesAsync(IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds)
    {
        var groups = groupIds.ToArray();
        var own = organizationIds.ToArray();
        return db.Organizations.AsNoTracking().Where(o => o.IsActive && (groups.Contains(o.ResidentialGroupId) || own.Contains(o.Id))).OrderBy(o => o.Name).ToListAsync();
    }

    public Task<List<OrganizationMember>> GetMembersAsync(long organizationId) =>
        db.OrganizationMembers.AsNoTracking().Include(m => m.Resident).Where(m => m.OrganizationId == organizationId && m.LeftAt == null).OrderBy(m => m.Resident.FullName).ToListAsync();

    public Task<List<Resident>> GetCandidatesAsync(Organization organization)
    {
        var orgId = organization.Id;
        var groupId = organization.ResidentialGroupId;
        return db.Residents.AsNoTracking().Where(r => r.Household != null && !r.Household.IsArchived && r.Household.ResidentialGroupId == groupId &&
            !db.OrganizationMembers.Any(m => m.OrganizationId == orgId && m.ResidentId == r.Id && m.LeftAt == null)).OrderBy(r => r.FullName).ToListAsync();
    }

    public Task<OrganizationMember?> FindMemberAsync(long organizationId, long residentId) =>
        db.OrganizationMembers.SingleOrDefaultAsync(m => m.OrganizationId == organizationId && m.ResidentId == residentId);

    public Task<bool> ResidentInGroupAsync(long residentId, long groupId) =>
        db.Residents.AnyAsync(r => r.Id == residentId && r.Household != null && !r.Household.IsArchived && r.Household.ResidentialGroupId == groupId);

    public Task<bool> CodeExistsAsync(string code) => db.Organizations.AnyAsync(o => o.Code == code);

    public void Add(Organization organization) => db.Organizations.Add(organization);

    public void AddMember(OrganizationMember member) => db.OrganizationMembers.Add(member);
}
