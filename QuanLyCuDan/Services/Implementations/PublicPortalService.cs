using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class PublicPortalService(IPublicPortalRepository portal, IResidentialUnitRepository units) : IPublicPortalService
{
    private const int PageSize = 9;

    public async Task<PublicHomeViewModel> GetHomeAsync(string? search, long? groupId, int page = 1)
    {
        search = search?.Trim();
        if (search?.Length > 200)
            search = search[..200];
        var result = await portal.SearchPublishedAsync(search, groupId, page, PageSize);
        return new PublicHomeViewModel
        {
            Search = search,
            GroupId = groupId,
            Page = result.Page,
            Total = result.Total,
            Groups = await units.GetRegistrableGroupsAsync(),
            Announcements = result.Items
        };
    }

    public Task<Announcement?> GetAnnouncementAsync(long id) => portal.FindPublishedAsync(id);
}
