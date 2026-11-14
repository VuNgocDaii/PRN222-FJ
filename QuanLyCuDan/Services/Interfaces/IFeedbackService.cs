using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IFeedbackService
{
    Task<FeedbackPageViewModel> GetAsync(ClaimsPrincipal actor, long? id, string? notice);
    Task<long> CreateAsync(ClaimsPrincipal actor, string subject, string category, string body, Stream? attachment, long attachmentLength, string attachmentFileName);
    Task ProcessAsync(ClaimsPrincipal actor, long id);
    Task CompleteAsync(ClaimsPrincipal actor, long id);
    Task ReplyAsync(ClaimsPrincipal actor, long id, string body);
    Task<DocumentDownload?> DownloadAttachmentAsync(ClaimsPrincipal actor, long id);
}
