namespace QuanLyCuDan.Models;

public sealed class FeedbackPageViewModel
{
    public bool CanSubmit { get; init; }
    public bool CanManage { get; init; }
    public string? Notice { get; init; }
    public IReadOnlyList<FeedbackListItem> Items { get; init; } = [];
    public FeedbackConversation? Selected { get; init; }
}

public sealed record FeedbackListItem(long Id, string Code, string Subject, string Status, DateTime SubmittedAt, string? ResidentName);
public sealed record FeedbackMessage(string Author, string Body, DateTime At, bool Official);
public sealed record FeedbackConversation(long Id, string Code, string Subject, string Category, string Status, DateTime SubmittedAt, string ResidentName, string Body, IReadOnlyList<FeedbackMessage> Messages, bool CanDownloadAttachment, string? AttachmentFileName);
