namespace QuanLyCuDan.Services.Interfaces;

// Đẩy thông báo thời gian thực (SignalR) khi góp ý thay đổi.
public interface IFeedbackNotifier
{
    Task ToStaffAsync(long groupId, long requestId, string message);
    Task ToUserAsync(string userId, long requestId, string message);
}
