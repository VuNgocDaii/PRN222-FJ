using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IResidentService
{
    Task Check(ClaimsPrincipal actor, string permission);
    Task<ResidentPortalViewModel> GetPortalAsync(ClaimsPrincipal actor, string section, int page = 1);
    Task<Announcement?> GetAnnouncementAsync(ClaimsPrincipal actor, long id);
    Task UpdateContactAsync(ClaimsPrincipal actor, ContactInput input);
    Task<long> CreateRequestAsync(ClaimsPrincipal actor, ResidentRequestInput input, Stream? attachment, long attachmentLength, string attachmentFileName);
    Task<ResidentRequestDetailViewModel?> GetDetailAsync(ClaimsPrincipal actor, long id);
    Task CancelAsync(ClaimsPrincipal actor, long id, string rowVersion);
    Task ReplyAsync(ClaimsPrincipal actor, long id, string body, string clientKey);
    Task MarkReadAsync(ClaimsPrincipal actor, long id);
    Task UploadAsync(ClaimsPrincipal actor, long id, Stream? content, long length, string fileName, string rowVersion);
    Task<DocumentDownload?> DownloadAsync(ClaimsPrincipal actor, long id);
}
