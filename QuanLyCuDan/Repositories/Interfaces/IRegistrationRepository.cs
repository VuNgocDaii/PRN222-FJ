using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IRegistrationRepository
{
    Task<List<AspNetUser>> ListByStatusAsync(string status, int take);
    Task<List<Resident>> GetUnlinkedResidentsAsync();
    Task<Dictionary<long, string>> GetHeadNamesAsync(IEnumerable<Resident> residents);
    // Hồ sơ cư dân khớp số CCCD và chưa liên kết với tài khoản nào khác.
    Task<Resident?> FindLinkableResidentAsync(string citizenId, string userId);
}
