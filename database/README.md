# Database

Chỉ dùng một script SQL đầy đủ: [`QuanLyCuDan.sql`](QuanLyCuDan.sql).

Script bao gồm schema nền, các migration, dữ liệu mẫu và cập nhật dữ liệu seed. Chạy toàn bộ file trên SQL Server bằng SSMS hoặc:

```powershell
sqlcmd -S localhost -E -C -b -f 65001 -i database/QuanLyCuDan.sql
```

Script nâng cấp theo `SchemaVersions`, không xóa database và không đặt lại mật khẩu khi chạy lại. 17 tài khoản mẫu được chuẩn hóa một lần về mật khẩu `12345678`; các tài khoản khác được giữ nguyên. Danh sách tài khoản: [`../TAI_KHOAN_TEST.md`](../TAI_KHOAN_TEST.md).

Nếu schema có nhiều role nghiệp vụ chưa thu hồi cho cùng một tài khoản, migration dừng và rollback để quản trị viên chọn role cần giữ. Không tự suy đoán role.

Kiểm thử tự cài và chạy lại script trên database riêng có tên chứa `VillageValidation`, sau đó chạy các luồng MVC, phân quyền, duyệt thông báo, đăng ký, cư dân và quản trị role:

```powershell
powershell -ExecutionPolicy Bypass -File .validation/test-notification-management.ps1 -SqlServer localhost
```

Runner không dùng database `QuanLyCuDan` đang chạy thật.
