using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuanLyCuDan.Controllers;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Hubs;

// Chỉ người đã đăng nhập mới kết nối được.
[Authorize(AuthenticationSchemes = "Cookies")]
public class NotificationHub : Hub
{
    private readonly MyStockContext _db;

    public NotificationHub(MyStockContext db)
    {
        _db = db;
    }

    // Hàm thử kết nối: chỉ gửi phản hồi về chính người gọi.
    public async Task Ping()
    {
        // Kiểm tra lại vì kết nối có thể vẫn mở sau khi đăng xuất ở tab khác.
        bool sessionIsValid = await BaseController.ValidSession(_db, Context.User!);
        if (!sessionIsValid)
        {
            Context.Abort();
            throw new HubException("Phiên đăng nhập đã hết. Hãy đăng nhập lại.");
        }

        await Clients.Caller.SendAsync("ReceiveNotification", "SignalR đã kết nối thành công.");
    }
}
