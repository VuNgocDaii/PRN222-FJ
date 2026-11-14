using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class RegistrationRepository(MyStockContext db) : IRegistrationRepository
{
    public Task<List<AspNetUser>> ListByStatusAsync(string status, int take) =>
        db.AspNetUsers.AsNoTracking().Where(u => u.VerificationStatus == status).OrderByDescending(u => u.CreatedAt).Take(take).ToListAsync();

    public Task<List<Resident>> GetUnlinkedResidentsAsync() =>
        db.Residents.AsNoTracking().Where(r => !db.AspNetUsers.Any(u => u.ResidentId == r.Id)).OrderBy(r => r.FullName).ToListAsync();

    public Task<Resident?> FindLinkableResidentAsync(string citizenId, string userId) =>
        db.Residents.SingleOrDefaultAsync(r => r.CitizenId == citizenId && !db.AspNetUsers.Any(u => u.ResidentId == r.Id && u.Id != userId));

    public async Task<Dictionary<long, string>> GetHeadNamesAsync(IEnumerable<Resident> residents)
    {
        var list = residents.ToList();
        var householdIds = list.Select(r => (long?)r.HouseholdId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var heads = await db.Households.AsNoTracking().Where(h => householdIds.Contains(h.Id))
            .Select(h => new { h.Id, Head = (long?)h.HeadResidentId }).ToListAsync();
        var headIds = heads.Where(x => x.Head != null).Select(x => x.Head!.Value).ToList();
        var names = await db.Residents.AsNoTracking().Where(r => headIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.FullName);
        var byHousehold = heads.Where(x => x.Head != null && names.ContainsKey(x.Head.Value)).ToDictionary(x => x.Id, x => names[x.Head!.Value]);
        return list.Where(r => r.HouseholdId != null && byHousehold.ContainsKey((long)r.HouseholdId)).ToDictionary(r => r.Id, r => byHousehold[(long)r.HouseholdId!]);
    }
}
