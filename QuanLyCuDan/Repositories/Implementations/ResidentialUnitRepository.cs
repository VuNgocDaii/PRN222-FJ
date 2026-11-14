using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class ResidentialUnitRepository(MyStockContext db) : IResidentialUnitRepository
{
    private IQueryable<ResidentialUnit> ActiveGroups() =>
        db.ResidentialUnits.AsNoTracking().Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.UnitType == "AREA" && g.ParentUnit.IsActive);

    public Task<List<ResidentialUnit>> GetActiveGroupsAsync(IReadOnlyCollection<long>? ids = null)
    {
        var query = ActiveGroups();
        if (ids != null)
        {
            var list = ids.ToArray();
            query = query.Where(g => list.Contains(g.Id));
        }
        return query.OrderBy(g => g.Name).ToListAsync();
    }

    public Task<bool> IsActiveGroupAsync(long id) => ActiveGroups().AnyAsync(g => g.Id == id);
    public Task<List<ResidentialUnit>> GetRegistrableGroupsAsync() =>
        db.ResidentialUnits.AsNoTracking().Where(g => g.IsActive && g.UnitType == "GROUP" && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA").OrderBy(g => g.Name).ToListAsync();

    public Task<bool> IsRegistrableGroupAsync(long? id) =>
        db.ResidentialUnits.AnyAsync(g => g.Id == id && g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA");

    public Task<List<long>> GetActiveGroupIdsUnderAreasAsync(IReadOnlyCollection<long> areaIds) =>
        db.ResidentialUnits.AsNoTracking().Where(x => x.IsActive && x.ParentUnitId != null && areaIds.Contains(x.ParentUnitId.Value)).Select(x => x.Id).ToListAsync();
}
