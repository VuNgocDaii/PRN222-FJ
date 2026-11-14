using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IOrganizationRepository
{
    // all = true: xem mọi tổ chức (quyền SYSTEM); ngược lại lọc theo thôn hoặc tổ chức được gán.
    Task<List<Organization>> GetAccessibleAsync(bool all, IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds);
    Task<Organization?> FindActiveAccessibleAsync(bool all, IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds, long id);
    // Danh sách tổ chức đang hoạt động để chọn khi soạn thông báo.
    Task<List<Organization>> GetActiveChoicesAsync(IReadOnlyCollection<long> groupIds, IReadOnlyCollection<long> organizationIds);
    Task<List<OrganizationMember>> GetMembersAsync(long organizationId);
    Task<List<Resident>> GetCandidatesAsync(Organization organization);
    Task<OrganizationMember?> FindMemberAsync(long organizationId, long residentId);
    Task<bool> ResidentInGroupAsync(long residentId, long groupId);
    Task<bool> CodeExistsAsync(string code);
    void Add(Organization organization);
    void AddMember(OrganizationMember member);
}
