namespace QuanLyCuDan.Models;

public sealed record ManagementModule(string Key, string Title, string Description, string[] Features)
{
    public static IReadOnlyList<ManagementModule> All { get; } = new ManagementModule[]
    {
        new("overview", "Tổng quan", "Theo dõi tình hình khu dân cư.", new[] { "Thống kê cư dân và hộ gia đình", "Yêu cầu đang chờ xử lý", "Tình hình thu phí" }),
        new("units", "Xã / Thôn", "Quản lý địa bàn và cơ cấu khu dân cư.", new[] { "Danh sách xã", "Danh sách thôn", "Thông tin và trạng thái địa bàn" }),
        new("households", "Hộ gia đình", "Quản lý hồ sơ hộ và thành viên.", new[] { "Danh sách hộ gia đình", "Chủ hộ và địa chỉ", "Thành viên trong hộ" }),
        new("residents", "Cư dân", "Quản lý hồ sơ và tình trạng cư trú.", new[] { "Danh sách cư dân", "Thông tin cá nhân", "Thường trú, tạm trú và chuyển đi" }),
        new("residence-requests", "Yêu cầu cư trú", "Tiếp nhận và xét duyệt thay đổi cư trú.", new[] { "Gửi yêu cầu cư trú", "Danh sách chờ duyệt", "Kết quả và lịch sử biến động" }),
        new("feedback", "Phản ánh / Góp ý", "Tiếp nhận và theo dõi phản ánh của cư dân.", new[] { "Gửi phản ánh, góp ý", "Tiếp nhận và xử lý", "Trao đổi và kết quả xử lý" }),
        new("fees", "Đợt thu phí", "Quản lý các đợt thu theo địa bàn.", new[] { "Danh sách đợt thu", "Mức thu và hộ tham gia", "Trạng thái đợt thu" }),
        new("payments", "Thanh toán / Thu tiền", "Theo dõi khoản thu của từng hộ.", new[] { "Khoản phải đóng", "Ghi nhận đóng tiền", "Lịch sử và biên lai" }),
        new("announcements", "Thông báo chung", "Quản lý thông tin công bố cho cư dân.", new[] { "Danh sách thông báo", "Soạn nháp và đăng bài", "Gỡ thông báo" }),
        new("notifications", "Thông báo cá nhân", "Theo dõi cập nhật dành cho tài khoản của bạn.", new[] { "Thông báo chưa đọc", "Đánh dấu đã đọc", "Cập nhật theo thời gian thực" }),
        new("reports", "Báo cáo / Thống kê", "Tổng hợp số liệu phục vụ quản lý.", new[] { "Thống kê dân cư", "Biến động cư trú", "Tổng hợp thu phí" }),
        new("activities", "Lịch sử hoạt động", "Tra cứu lịch sử thao tác theo quyền truy cập.", new[] { "Lịch sử xử lý nghiệp vụ", "Nhật ký quản trị" })
    };
}
