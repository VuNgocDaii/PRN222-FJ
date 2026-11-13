using System.ComponentModel.DataAnnotations;
namespace QuanLyCuDan.Models;
public sealed class PublicHomeViewModel
{
    public List<Announcement> Announcements { get; set; } = [];
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public OrganizationDirectoryViewModel Organizations { get; set; } = new();
}
public sealed class ResidentPortalViewModel
{
    public string Section { get; set; } = "overview";
    public AspNetUser Account { get; set; } = null!;
    public HashSet<string> Access { get; set; } = [];
    public List<Resident> Members { get; set; } = [];
    public List<Request> Requests { get; set; } = [];
    public List<Announcement> Announcements { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<Activity> Notifications { get; set; } = [];
    public bool IsOnboarding { get; set; }
    public OrganizationDirectoryViewModel Organizations { get; set; } = new();
    public MyOrganizationsViewModel MyOrganizations { get; set; } = new();
    public ResidentRequestInput Input { get; set; } = new();
    public int Page { get; set; } = 1;
    public int Total { get; set; }
}
public sealed class ResidentRequestInput
{
    [Required, RegularExpression("^(RESIDENCE|FEEDBACK)$")] public string Kind { get; set; } = "RESIDENCE";
    [Required, StringLength(30)] public string Type { get; set; } = "TEMPORARY_ABSENCE";
    [Required, StringLength(250, MinimumLength = 3)] public string Subject { get; set; } = "";
    [Required, StringLength(4000, MinimumLength = 5)] public string Body { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [StringLength(30)] public string? TargetHouseholdCode { get; set; }
    public long? NewHeadResidentId { get; set; }
    [Required, RegularExpression("^[a-fA-F0-9]{32}$")] public string ClientKey { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class ContactInput
{
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Số điện thoại phải có 9–15 chữ số.")]
    public string PhoneNumber { get; set; } = "";
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Required] public string RowVersion { get; set; } = "";
    [Required] public string ResidentVersion { get; set; } = "";
}
public sealed class ResidentRequestDetailViewModel
{
    public Request Request { get; set; } = null!;
    public List<Activity> Timeline { get; set; } = [];
    public bool CanCancel { get; set; }
    public bool CanReply { get; set; }
    public bool CanUpload { get; set; }
    public bool CanDownload { get; set; }
}
public static class PortalLabels
{
    public static string Text(string value) => value switch
    {
        "PENDING" => "Chờ tiếp nhận", "PROCESSING" => "Đang xử lý", "APPROVED" => "Đã duyệt",
        "REJECTED" => "Từ chối", "CANCELLED" => "Đã hủy", "RESPONDED" => "Đã trả lời",
        "CLOSED" => "Đã đóng", "OUT_OF_SCOPE" => "Ngoài phạm vi", "RESIDING" => "Thường trú",
        "TEMPORARY" => "Tạm trú", "ABSENT" => "Tạm vắng", "MOVED_OUT" => "Đã chuyển đi",
        "MOVE_IN" => "Chuyển đến", "MOVE_OUT" => "Chuyển đi", "TEMPORARY_RESIDENCE" => "Đăng ký tạm trú",
        "TEMPORARY_ABSENCE" => "Đăng ký tạm vắng", "ADD_MEMBER" => "Đăng ký vào hộ", "CHANGE_HEAD" => "Thay đổi chủ hộ",
        "FEEDBACK" => "Phản ánh", "ENVIRONMENT" => "Môi trường", "INFRASTRUCTURE" => "Hạ tầng", "OTHER" => "Khác",
        "MALE" => "Nam", "FEMALE" => "Nữ", "PUBLIC" => "Công khai", "RESIDENTS" => "Dành cho cư dân",
        "REQUIRED" => "Bắt buộc", "VOLUNTARY" => "Tự nguyện", "CASH" => "Tiền mặt", "TRANSFER" => "Chuyển khoản",
        "SUBMITTED" => "Đã gửi", "RESIDENT_CANCELLED" => "Cư dân hủy yêu cầu", "RESIDENT_MESSAGE" => "Trao đổi của cư dân",
        "FEEDBACK_REPLIED" => "Phản ánh của bạn đã được trả lời", "ANNOUNCEMENT_PUBLISHED" => "Địa bàn có thông báo mới",
        "PAYMENT_RECORDED" => "Khoản đóng tiền của hộ đã được ghi nhận", "REQUEST_APPROVED" => "Yêu cầu đã được duyệt",
        "REQUEST_REJECTED" => "Yêu cầu đã bị từ chối", "ATTACHMENT_UPLOADED" => "Đã bổ sung giấy tờ",
        _ => value
    };
}
