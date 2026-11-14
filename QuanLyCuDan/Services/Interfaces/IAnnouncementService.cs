using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IAnnouncementService
{
    Task<AnnouncementListViewModel> ListAsync(ClaimsPrincipal actor, string? search, string filter = "ALL", long? groupId = null, int page = 1);
    Task<AnnouncementEditViewModel> GetCreateModelAsync(ClaimsPrincipal actor, AnnouncementInput? input = null);
    Task<long> CreateAsync(ClaimsPrincipal actor, AnnouncementInput input);
    Task<AnnouncementDetailViewModel?> GetDetailAsync(ClaimsPrincipal actor, long id);
    Task<AnnouncementEditViewModel?> GetEditModelAsync(ClaimsPrincipal actor, long id, AnnouncementInput? input = null);
    Task UpdateAsync(ClaimsPrincipal actor, long id, AnnouncementInput input);
    Task<string> SubmitAsync(ClaimsPrincipal actor, long id, string rowVersion);
    Task<string> WithdrawAsync(ClaimsPrincipal actor, long id, string rowVersion);
    Task<string> ReviewAsync(ClaimsPrincipal actor, long id, string rowVersion, bool approve, string? note);
    Task<string> UnpublishAsync(ClaimsPrincipal actor, long id, string rowVersion, string? reason);
    Task UploadDocumentAsync(ClaimsPrincipal actor, long id, Stream? content, long length, string fileName, string rowVersion);
    Task<DocumentDownload?> DownloadDocumentAsync(ClaimsPrincipal actor, long id);
    IReadOnlyDictionary<string, string> ValidateContent(AnnouncementInput input);
}
