using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IResidentialUnitRepository
{
    // Thôn (UnitType = GROUP) đang hoạt động; ids = null nghĩa là tất cả.
    Task<List<ResidentialUnit>> GetActiveGroupsAsync(IReadOnlyCollection<long>? ids = null);
    Task<bool> IsActiveGroupAsync(long id);
    // Thôn có thể đăng ký: GROUP đang hoạt động, thuộc AREA đang hoạt động.
    Task<List<ResidentialUnit>> GetRegistrableGroupsAsync();
    Task<bool> IsRegistrableGroupAsync(long? id);
    Task<List<long>> GetActiveGroupIdsUnderAreasAsync(IReadOnlyCollection<long> areaIds);
}
