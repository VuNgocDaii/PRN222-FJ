using System.ComponentModel.DataAnnotations;

namespace QuanLyCuDan.Models;

public sealed class AnnouncementInput
{
    [Required(ErrorMessage = "Nhập tiêu đề."), StringLength(250)] public string Title { get; set; } = "";
    [Required(ErrorMessage = "Nhập nội dung."), StringLength(20000)] public string Body { get; set; } = "";
    [Range(1, long.MaxValue)] public long ResidentialGroupId { get; set; }
    [Required, RegularExpression("^(PUBLIC|RESIDENTS)$")] public string Audience { get; set; } = "RESIDENTS";
    // Browser datetime-local values represent Vietnam time, converted explicitly on the server.
    public DateTime? ExpiresAtLocal { get; set; }
    public string RowVersion { get; set; } = "";
}

public sealed class AnnouncementListViewModel
{
    public List<Announcement> Items { get; set; } = [];
    public List<ResidentialUnit> Groups { get; set; } = [];
    public string? Search { get; set; }
    public string Filter { get; set; } = "ALL";
    public long? GroupId { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public bool CanCreate { get; set; }
}

public sealed class AnnouncementEditViewModel
{
    public long? Id { get; set; }
    public AnnouncementInput Input { get; set; } = new();
    public List<ResidentialUnit> Groups { get; set; } = [];
}

public sealed class AnnouncementDetailViewModel
{
    public Announcement Item { get; set; } = null!;
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanReview { get; set; }
    public bool CanPublish { get; set; }
    public bool CanUnpublish { get; set; }
    public bool CanWithdraw { get; set; }
    public List<AnnouncementHistoryItem> History { get; set; } = [];
}

public sealed record AnnouncementHistoryItem(string EventCode, string? Actor, DateTime OccurredAt);

public static class AnnouncementLabels
{
    public static string Review(string status) => status switch { "PENDING" => "Chờ duyệt", "APPROVED" => "Đã duyệt", "REJECTED" => "Từ chối", _ => "Nháp" };
    public static string Status(string status) => status switch { "PUBLISHED" => "Đã đăng", "UNPUBLISHED" => "Đã gỡ", _ => "Chưa đăng" };
    public static string Event(string code) => code switch {
        "ANNOUNCEMENT_CREATED" => "Soạn nháp", "ANNOUNCEMENT_UPDATED" => "Sửa nội dung, yêu cầu duyệt lại",
        "ANNOUNCEMENT_SUBMITTED" => "Gửi duyệt", "ANNOUNCEMENT_APPROVED" => "Duyệt thông báo", "ANNOUNCEMENT_REJECTED" => "Từ chối duyệt",
        "ANNOUNCEMENT_PUBLISHED" => "Đăng thông báo", "ANNOUNCEMENT_UNPUBLISHED" => "Gỡ thông báo", "ANNOUNCEMENT_WITHDRAWN" => "Rút lại để chỉnh sửa", _ => code };
}
