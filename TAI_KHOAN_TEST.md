# Danh sách tài khoản test

Script tạo lại đầy đủ schema và dữ liệu: **[Reset_QuanLyCuDan.sql](Reset_QuanLyCuDan.sql)**.

**Mật khẩu chung: `12345678`**. Có 17 tài khoản, mỗi tài khoản có một role hiện hành. Khách truy cập không cần tài khoản.

| Tài khoản | Mật khẩu | Họ tên | Chức danh | Địa bàn / tổ chức |
|---|---|---|---|---|
| admin | 12345678 | Nguyễn Thành Trung | Quản trị viên | Toàn hệ thống |
| truongthon | 12345678 | Nguyễn Văn Hòa | Trưởng thôn | Thôn An Hòa |
| bithu | 12345678 | Trần Thị Lan | Bí thư chi bộ | Thôn An Hòa |
| hoiphunu | 12345678 | Lê Thị Mai | Trưởng tổ chức | Chi hội Phụ nữ thôn An Hòa |
| chidoan | 12345678 | Phạm Minh Tuấn | Trưởng tổ chức | Chi đoàn thôn An Hòa |
| cappho | 12345678 | Đặng Thị Hạnh | Cấp phó | Chi hội Phụ nữ thôn An Hòa |
| phothon | 12345678 | Võ Văn Bình | Cấp phó | Thôn An Hòa |
| canboxa | 12345678 | Bùi Thanh Sơn | Cán bộ xã | Xã Minh An |
| cudan | 12345678 | Nguyễn Thị Thu | Cư dân | Thôn An Hòa, thành viên Chi hội Phụ nữ |
| cudan_khac | 12345678 | Hoàng Văn Nam | Cư dân | Thôn Bình Minh |
| canbo01 | 12345678 | Đặng Văn Hải | Cán bộ | Tổ dân phố số 1, khu dân cư Minh Hòa |
| canbo02 | 12345678 | Trần Quốc Hùng | Cán bộ | Tổ dân phố số 2, khu dân cư Minh Hòa |
| thuphi01 | 12345678 | Phạm Thị Hồng | Cán bộ thu phí | Tổ dân phố số 1, khu dân cư Minh Hòa |
| dan01 | 12345678 | Nguyễn Văn An | Cư dân, chủ hộ | Tổ dân phố số 1, khu dân cư Minh Hòa |
| dan02 | 12345678 | Trần Thị Bình | Cư dân, thành viên hộ | Tổ dân phố số 1, khu dân cư Minh Hòa |
| dan03 | 12345678 | Lê Văn Cường | Cư dân | Tổ dân phố số 2, khu dân cư Minh Hòa |
| pending01 | 12345678 | Đỗ Thị Hương | Tài khoản chờ duyệt | Chưa liên kết hồ sơ cư dân |

`pending01` bị chặn đăng nhập cho đến khi quản trị viên duyệt. Các tài khoản còn lại đăng nhập được ngay sau khi chạy script.

## Các trường hợp test chính

- `hoiphunu`, `chidoan`, `cappho`, `phothon`: soạn và gửi duyệt; không được duyệt/gỡ.
- `truongthon`, `bithu`: một người duyệt là thông báo tự đăng; được tự duyệt và gỡ bài của nhau trong thôn.
- `cudan` và `cudan_khac`: kiểm tra cách ly thông báo theo thôn. `cudan` nhận thông báo Chi hội Phụ nữ; `chidoan` không được đọc bản nháp riêng của chi hội đó.
- `canboxa`: nhập chỉ thị cho thôn thuộc xã, kèm hạn thực hiện và văn bản PDF.
- `admin`: quản lý tài khoản, chức danh, tổ chức và xét duyệt đăng ký; không mặc nhiên có quyền duyệt thông báo nghiệp vụ.
- `dan01` và `dan02`: đối chiếu quyền chủ hộ/thành viên khi tra cứu khoản thu.
- Khách: xem thông báo công khai, tìm kiếm và lọc địa bàn trên trang chủ.

## Chạy script

Từ thư mục gốc repo:

```powershell
sqlcmd -S localhost -E -C -b -f 65001 -i Reset_QuanLyCuDan.sql
```

Thay `localhost` bằng `.\SQLEXPRESS` nếu cần. Có thể mở file SQL trong SSMS và chạy toàn bộ.

**Script xóa toàn bộ DB `QuanLyCuDan` rồi tạo lại.** Chỉ chạy khi muốn thay toàn bộ dữ liệu. Danh sách trên áp dụng sau khi script hoàn tất; dữ liệu được tổng hợp để kiểm thử, không nhập từ nguồn thông tin cá nhân thực.
