# Rà soát giao diện — 13/11/2026

## Cập nhật giao diện admin theo bố cục cũ

Đã khôi phục `/Admin` về ba mục Tài khoản / Vai trò / Quyền truy cập: bảng tài khoản có tìm kiếm, phân trang và dialog sửa tên; phần chọn tài khoản và gán/thu hồi vai trò; bộ chọn vai trò, form thông tin và lưới quyền; form thêm/sửa quyền và bảng danh mục quyền. Nhãn xác minh, trạng thái tạm khóa và các bảo vệ vai trò/quyền đang được sử dụng được giữ theo giao diện cũ. Dữ liệu render từ MVC; các form POST quay về đúng tab và hiển thị kết quả, không gọi API.

Build và kiểm thử CRUD/validation/return URL đạt. Chrome kiểm tra các tab và trạng thái sửa quyền ở 1440, 390, 320 px; dialog sửa tên/xác nhận, phạm vi gán vai trò, JWT localStorage và không có request API. Ảnh đối chiếu lưu tại `QuanLyCuDan/.local/admin-restored`. Bộ kiểm tra: `.validation/admin-browser.cjs`.

Đã kiểm tra 38 màn hình/trạng thái với tài khoản quản trị, cán bộ và cư dân trên Chrome ở độ rộng 1440, 390 và 320 px (114 lượt kiểm tra). Phạm vi gồm trang công khai, đăng nhập/đăng ký, tài khoản, quản trị tài khoản/vai trò/quyền, gán vai trò, ba trạng thái duyệt đăng ký, các mục Management, thông báo, góp ý và các mục trong cổng cư dân. Có kiểm tra thêm dialog đổi mật khẩu và dialog xem đăng ký.

## Các lỗi đã sửa

| Khu vực | Vấn đề | Thay đổi |
| --- | --- | --- |
| Tài khoản | Chữ tiếng Việt bị lỗi mã hóa | Sửa nội dung UTF-8 của Razor View |
| Quản trị | Nút điều hướng tràn ngang trên mobile; màu chữ/nền khó đọc | Cho phép xuống hàng và sửa màu nút |
| Tài khoản, vai trò, quyền | Checkbox rộng bằng ô nhập, cao 43 px | Checkbox 18 px, nhãn nằm cạnh, vùng nhấn đủ rộng |
| Form quản trị | Ô nhập trải quá rộng, nhóm phạm vi thiếu bố cục | Giới hạn chiều rộng form, bố trí fieldset và danh sách quyền responsive |
| Gán vai trò | Cho chọn phạm vi/địa bàn không tương thích | Lọc phạm vi theo quyền của vai trò; lọc địa bàn AREA/GROUP; bỏ chọn địa bàn với SYSTEM/SELF |
| Gán vai trò | Thời hạn và trạng thái trống khó hiểu | Kiểm tra ngày kết thúc; hiển thị tên địa bàn, thời hạn và hướng dẫn chọn tài khoản |
| Điều hướng | Không đánh dấu mục quản trị đang mở | Thêm trạng thái active và aria-current |
| Góp ý | Layout mẫu Home/Privacy khác hệ thống; badge kết nối trắng trên nền nhạt | Đồng bộ nhãn, thanh điều hướng, footer và màu; sửa độ tương phản badge |
| Tài khoản | Hướng dẫn đổi mật khẩu không khớp form hiện có | Sửa hướng dẫn; kiểm tra xác nhận mật khẩu ngay trên form |
| Duyệt đăng ký | Thông báo thành công bị hiển thị hai lần | Tránh render thông báo trùng từ layout và view |

## Xác minh

- Build thành công, 0 lỗi và 0 cảnh báo biên dịch.
- Lượt rà soát 38 màn hình: không có tràn ngang toàn trang, ô nhập thiếu nhãn hoặc lỗi JavaScript được ghi nhận. Bảng dữ liệu rộng vẫn cuộn bên trong vùng bảng.
- Kiểm tra riêng thao tác chọn vai trò/phạm vi/địa bàn và xác nhận mật khẩu.
- Kiểm thử HTTP Razor View và kiểm thử tạo/sửa tài khoản, vai trò, quyền, gán/thu hồi quyền, concurrency và đổi mật khẩu đều đạt; fixture đã được dọn.
- Kiểm tra hình ảnh bổ sung xác nhận màu nút quản trị, kích thước checkbox và badge kết nối góp ý.

Ảnh và báo cáo máy đọc lưu tại `QuanLyCuDan/.local/ui-review-before`, `ui-review-after` và `ui-review-focus` (không đưa vào Git). Có ảnh tổng hợp `contact-1440.png` và `contact-390.png` trong thư mục `ui-review-after`.

## Phần còn giới hạn

Các mục Management như địa bàn, hồ sơ cư dân/hộ, xử lý cư trú, đợt thu, thu tiền, báo cáo và lịch sử hiện vẫn là trang “chức năng dự kiến”. Lượt sửa này không bổ sung nghiệp vụ chưa được triển khai. Các trạng thái dữ liệu chỉ xuất hiện với hồ sơ hoặc quyền đặc biệt không được coi là đã kiểm thử đầy đủ chỉ dựa trên các tài khoản demo.

Chạy lại với ứng dụng dùng dữ liệu demo cục bộ:

```powershell
$env:AUTH_TEST_URL = 'http://127.0.0.1:5099'
$env:UI_REVIEW_OUTPUT = 'ui-review-after'
$env:UI_REVIEW_ASSERT = '1'
node .validation/ui-review.cjs
```

Yêu cầu Chrome cục bộ; có thể đặt `CHROME_PATH`. Bộ kiểm tra chỉ đăng nhập, mở trang và thao tác trên trình duyệt, không gửi form thay đổi nghiệp vụ.
