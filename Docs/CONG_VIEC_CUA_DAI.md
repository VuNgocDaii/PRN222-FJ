# Tổng hợp công việc của Đại trong QuanLyCuDan

Ngày đối chiếu: 13/11/2026. Tài khoản Git: `daivn@pmr.vn`.

Tài liệu tổng hợp theo lịch sử Git và mã nguồn tại các commit tương ứng. “Phần của Đại” ở đây là phần được thêm hoặc thay đổi trong commit đứng tên tài khoản trên; Git không xác minh người trực tiếp viết từng dòng. File được thêm ở commit đầu có thể đã được thành viên khác sửa sau đó. Thư viện bên thứ ba, file sinh tự động và dự án mẫu không được tính là chức năng tự viết.

## 1. Tổng quan phạm vi đóng góp

- Khởi tạo ứng dụng ASP.NET Core MVC, kết nối SQL Server qua Entity Framework Core, cấu hình middleware và định tuyến.
- Xây dựng xác thực MVC bằng cookie và API bằng JWT, quản lý phiên, refresh token và đổi mật khẩu.
- Xây dựng quản trị tài khoản, vai trò, quyền và gán vai trò theo phạm vi.
- Xây dựng đăng ký tài khoản và duyệt đăng ký ở phiên bản ban đầu.
- Xây dựng giao diện quản trị, các trang tài khoản và JavaScript gọi API.
- Đưa vào các entity, DbContext, script tạo database và dữ liệu mẫu của phiên bản nền.
- Xây dựng nền tảng SignalR có xác thực và trang kiểm tra kết nối.
- Tích hợp nhánh của các thành viên; cập nhật script nâng cấp database; bổ sung script chạy kiểm thử thông báo.
- Bổ sung lưu access token vào localStorage và kiểm thử liên quan.

## 2. Toàn bộ 14 controller được thêm trong commit gốc

Nguồn: commit `5f5b1dd` — `Add controller-based resident management application`, ngày 03/11/2026. Trong đó có 12 controller cụ thể và 2 lớp controller nền.

| Controller | Công việc ở phiên bản ban đầu |
| --- | --- |
| `AccountController` | Trang đăng ký, tiếp nhận đăng ký, đăng nhập MVC, đăng xuất và trang hồ sơ tài khoản. |
| `AdminController` | Trang tổng quan quản trị. |
| `AuthController` | API cấp CSRF token, đăng nhập, refresh token, đăng xuất và đổi mật khẩu. |
| `AuthControllerBase` | Logic xác thực dùng chung: kiểm tra đăng nhập, tạo phiên, đăng nhập cookie, tạo/đổi token, đăng xuất và đổi mật khẩu. |
| `BaseController` | Kiểm tra người dùng và phiên, quyền hiệu lực, phạm vi nhóm/khu vực, ghi audit, băm/kiểm tra mật khẩu, bảo vệ admin cuối cùng và kiểm soát phiên bản dữ liệu. |
| `HomeController` | Các trang Home, Privacy và Error ở bản nền. |
| `ManagementController` | Điều hướng và hiển thị khung các phân hệ quản lý theo module. |
| `MeController` | API thông tin tài khoản hiện tại và quyền của người đang đăng nhập. |
| `PermissionsController` | Danh sách, tạo, cập nhật và xóa quyền với các điều kiện kiểm tra tương ứng. |
| `RegistrationsController` | Danh sách đăng ký theo trạng thái và xử lý duyệt/từ chối bản đầu. |
| `RoleAssignmentsController` | Xem, gán và thu hồi vai trò của tài khoản; cung cấp danh sách đơn vị phục vụ chọn phạm vi. |
| `RolesController` | Danh sách, tạo/cập nhật vai trò, lấy và cập nhật quyền của vai trò. |
| `SignalRController` | Hiển thị trang kiểm tra kết nối SignalR cho người đã đăng nhập. |
| `UsersController` | Danh sách tài khoản có tìm kiếm/phân trang, tạo tài khoản, sửa thông tin và trạng thái tài khoản. |

Các file nằm trong `QuanLyCuDan/Controllers/`. Việc controller thuộc commit gốc không có nghĩa toàn bộ phiên bản hiện tại của controller đều do Đại làm; ví dụ `AccountController`, `RegistrationsController` và `HomeController` được mở rộng ở các nhánh sau.

## 3. Xác thực và quản lý phiên

### Backend

- `Auth/AuthContracts.cs`: các kiểu dữ liệu đầu vào/đầu ra cho đăng nhập, token, đổi mật khẩu, tài khoản, vai trò, quyền và gán vai trò; có validation.
- `Auth/RegisterInput.cs`: dữ liệu đăng ký tài khoản ở phiên bản đầu.
- `Auth/TokenIssuer.cs`: phát hành JWT cho người dùng và phiên.
- `Auth/BearerHandler.cs`: xử lý xác thực Bearer token cho API.
- `AuthControllerBase`: chia sẻ logic giữa đăng nhập MVC và API, tránh phải gọi controller thông qua DI.
- `BaseController`: kiểm tra phiên còn hợp lệ, băm và xác minh mật khẩu.
- `Program.cs`: cấu hình cookie/Bearer, antiforgery, giới hạn tần suất đăng nhập/refresh và xử lý lỗi tập trung.

### Hành vi chính

- MVC sử dụng cookie đăng nhập; API trả access token và đặt refresh token trong cookie HttpOnly.
- Theo dõi phiên trong `UserSession` và refresh token trong `RefreshToken`.
- Kiểm tra tài khoản bị vô hiệu hóa, khóa hoặc chưa được duyệt trước khi cho đăng nhập.
- Khóa đăng nhập tạm thời khi nhập sai mật khẩu nhiều lần.
- Làm mới token theo cơ chế rotation và kiểm tra việc sử dụng token.
- Đổi mật khẩu và thu hồi phiên theo logic backend.
- Hỗ trợ xác minh hash SHA256 của dữ liệu demo và nâng cấp sang định dạng hash Identity khi đăng nhập thành công.

### Frontend và thay đổi sau tích hợp

- `wwwroot/js/auth-client.js`: gọi API có Bearer token, làm mới token, giới hạn số lần thử lại và xử lý hết phiên.
- Commit `481b12e` bổ sung lưu access token cùng thời hạn vào `localStorage` với khóa `qlcd.accessToken`, nạp lại token, xóa token khi đăng xuất/đổi mật khẩu/hết phiên.
- Commit trên cập nhật trang đăng nhập, layout quản trị, layout portal và test xác thực. Việc sửa layout portal ở commit này không đồng nghĩa Đại xây dựng toàn bộ portal.

## 4. Quản trị và phân quyền

- Quản lý tài khoản: danh sách, tìm kiếm, phân trang, tạo và cập nhật trạng thái/thông tin.
- Quản lý vai trò: danh sách, tạo, cập nhật và cấu hình quyền.
- Quản lý danh mục quyền và các phạm vi được phép áp dụng.
- Gán vai trò cho người dùng với phạm vi và thời gian hiệu lực; thu hồi có lý do.
- Kiểm tra quyền từ `VwEffectivePermission` và các bảng liên quan.
- Hỗ trợ các phạm vi `SYSTEM`, `AREA`, `GROUP`, `SELF` theo hợp đồng dữ liệu và quyền.
- Bảo vệ tài khoản/quyền quản trị cuối cùng trong các thao tác liên quan.
- Kiểm tra phiên bản bản ghi để hạn chế ghi đè khi nhiều người cùng sửa.
- Ghi lịch sử thao tác quản trị bằng `Activity`.

## 5. Đăng ký và duyệt tài khoản bản đầu

- Form đăng ký và tiếp nhận tài khoản đăng ký ở `AccountController`.
- Model đầu vào `RegisterInput`, view đăng ký và validation.
- Danh sách theo trạng thái và xử lý duyệt/từ chối ở `RegistrationsController`.
- `RegistrationListViewModel`, view danh sách và `registrations.js` hỗ trợ giao diện.

Phần hồ sơ cư dân chi tiết dưới dạng `RegistrationProfile`, liên kết/tạo hồ sơ cư dân khi duyệt và luồng onboarding mở rộng được bổ sung trong commit của Luan, không tính là toàn bộ công việc bản đầu của Đại.

## 6. SignalR do Đại đưa vào

- `Hubs/NotificationHub.cs`: yêu cầu xác thực cookie; hàm `Ping()` kiểm tra lại phiên và gửi `ReceiveNotification` về chính người gọi.
- `Controllers/SignalRController.cs`: trang thử SignalR chỉ dành cho người đã đăng nhập.
- `Views/SignalR/Index.cshtml`: trạng thái kết nối, nút gửi thử và danh sách phản hồi.
- `wwwroot/js/notification-client.js`: client dùng chung, kết nối tới Hub, nhận thông báo, tự kết nối lại và thông báo trạng thái.
- `Program.cs`: đăng ký SignalR, map `/hubs/notifications`, đóng kết nối khi xác thực hết hạn.

Phạm vi bản đầu là nền tảng kết nối có xác thực và kiểm tra gửi/nhận. Phần cập nhật góp ý theo thời gian thực tới nhóm cán bộ và cư dân được Nam bổ sung sau.

Lưu ý phiên bản: `Ping()`, `SignalRController` và trang `/SignalR` ở đây mô tả commit nền `5f5b1dd`. Trong mã hiện tại sau tích hợp, Hub dùng `OnConnectedAsync()` để kiểm tra phiên và chia nhóm góp ý, không còn `Ping()` hay controller/trang thử `/SignalR`. Client vẫn có handler `ReceiveNotification`, nhưng điều đó không có nghĩa Hub hiện tại còn phát sự kiện thử này. Xem mục 16 để hiểu luồng đang chạy.

## 7. Giao diện và JavaScript

### View được thêm ở bản đầu

- Tài khoản: `Views/Account/Login.cshtml`, `Register.cshtml`, `Profile.cshtml`.
- Quản trị: `Views/Admin/Index.cshtml`.
- Khung phân hệ quản lý: `Views/Management/Index.cshtml`.
- Duyệt đăng ký: `Views/Registrations/Index.cshtml`.
- Kiểm tra SignalR: `Views/SignalR/Index.cshtml`.
- Trang nền: `Views/Home/Index.cshtml`, `Privacy.cshtml`.
- Layout và view dùng chung: `_AccountLayout`, `_ManagementLayout`, `_Layout`, `Error`, `_ValidationScriptsPartial`, `_ViewImports`, `_ViewStart` và CSS của layout.

### File frontend ở bản đầu

| File trong `wwwroot` | Vai trò |
| --- | --- |
| `css/admin.css`, `css/site.css` | Giao diện quản trị và giao diện chung. |
| `js/admin.js` | Tương tác các màn hình quản trị. |
| `js/auth-client.js` | Xác thực và gọi API từ trình duyệt. |
| `js/account-display.js` | Hiển thị thông tin tài khoản. |
| `js/action-dialog.js` | Hộp thoại thao tác dùng chung. |
| `js/password-dialog.js` | Giao diện đổi mật khẩu. |
| `js/profile.js` | Xử lý giao diện hồ sơ. |
| `js/registrations.js` | Tương tác duyệt đăng ký. |
| `js/notification-client.js` | Kết nối SignalR dùng chung. |
| `js/site.js` | File JavaScript nền của ứng dụng. |

Các mục menu quản lý cư dân, hộ, thu phí, yêu cầu cư trú, thông báo, báo cáo… trong `ManagementModule` là danh mục/khung phân hệ ở commit đầu. Không dùng danh sách menu này làm bằng chứng rằng mọi nghiệp vụ đã được Đại triển khai hoàn chỉnh.

## 8. Model, DbContext và database

Commit gốc đưa vào `MyStockContext` và các model sau:

| Nhóm | Model |
| --- | --- |
| Tài khoản và Identity | `AspNetUser`, `AspNetRole`, `AspNetRoleClaim`, `AspNetUserClaim`, `AspNetUserLogin`, `AspNetUserToken` |
| Phân quyền | `Permission`, `PermissionAllowedScope`, `UserRoleAssignment`, `VwEffectivePermission` |
| Phiên và token | `UserSession`, `RefreshToken`, `PasswordResetChallenge` |
| Dữ liệu cư dân | `ResidentialUnit`, `Household`, `Resident`, `Request` |
| Thu phí và hoạt động | `FeeCampaign`, `Payment`, `Activity`, `Announcement` |
| Metadata và view SQL | `SchemaVersion`, `VwFeeBalance`, `VwPopulationChange` |
| Model giao diện | `ErrorViewModel`, `ManagementModule`, `RegistrationListViewModel` |

Đưa vào model/bảng là đóng góp nền dữ liệu; không đồng nghĩa có đầy đủ màn hình và luồng xử lý cho từng bảng.

Các file nền khác gồm `QuanLyCuDan.sln`, `QuanLyCuDan.csproj`, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json`, `.gitignore`, `.editorconfig`, hướng dẫn repo và README.

### Script database

- `QuanLyCuDan.sql`: script tạo database COMPACT-2.0, bảng, ràng buộc, view, quyền và dữ liệu demo ở bản nền.
- Commit `91686d6`: cập nhật script chính để chạy lại trên COMPACT-2.0 sẽ tiếp tục áp dụng nâng cấp đăng ký và duyệt thông báo; giữ nguyên dữ liệu/mật khẩu; đồng thời cập nhật README.
- Hai script nâng cấp độc lập `database/20261109_RegistrationProfile.sql` và `database/20261109_AnnouncementReview.sql` đã có trong phần của Luan. Đại tích hợp nội dung nâng cấp vào script chạy chính, không phải tác giả gốc của hai script đó.
- Trong phiên hỗ trợ đã chạy nâng cấp trên database local và kiểm tra các cột `RegistrationProfile`, `ReviewStatus`, `SubmittedAt`, `ReviewedByUserId`, `ReviewedAt`, `ReviewNote`.

## 9. Kiểm thử và hướng dẫn chạy

- Bản nền có `.validation/auth-client.test.cjs`, `.validation/auth-flow-smoke.cjs` và `.validation/controllers-smoke.cjs`.
- Commit `481b12e` cập nhật test liên quan đến lưu và sử dụng token phía trình duyệt.
- Commit `eee9680` thêm `.validation/test-notification-management.ps1` và hướng dẫn README.
- Script PowerShell kiểm tra công cụ, nâng cấp DB demo, build ứng dụng, chạy test auth client, khởi động app ở cổng riêng, gọi suite thông báo, dừng app và khôi phục biến môi trường.
- Suite `.validation/announcements-smoke.cjs` đã có từ phần tích hợp trước; đóng góp ở `eee9680` là runner tự động hóa, không phải viết mới toàn bộ suite.

Lệnh chạy:

```powershell
powershell -ExecutionPolicy Bypass -File .validation/test-notification-management.ps1
```

Kết quả đã xác nhận trong phiên làm việc: build không lỗi/cảnh báo, 6 test auth client thành công, suite thông báo thành công và dọn fixture. Suite kiểm tra soạn/sửa/gửi duyệt/rút/từ chối/duyệt/đăng/gỡ, phân quyền GROUP/AREA, CSRF/XSS, concurrency, nội dung công khai/nội bộ/hết hạn, đúng người nhận, chống đăng lặp và audit. Đây là kết quả tại thời điểm chạy, không phải cam kết mọi phiên bản sau đều đã kiểm thử.

## 10. Công việc tích hợp nhánh

| Commit đứng tên Đại | Nội dung |
| --- | --- |
| `b5ca228` | Merge `feature/phan-cua-Luan` vào main. |
| `3c7b553` | Merge luồng góp ý của Nam vào main. |
| `69eb164` | Merge `feature_notificationManagement` vào main; không có xung đột. |

Các chức năng gốc của thành viên khác:

| Thành viên theo Git | Commit | Phạm vi |
| --- | --- | --- |
| Luan | `9d73f0a` | Resident portal, đăng ký/xác minh cư dân mở rộng và phần duyệt thông báo. |
| Phan nAM | `b1bc4e9` | Luồng góp ý riêng tư và cập nhật thời gian thực bằng SignalR. |
| Hung-18 | `7462f0f` | Quản lý thông báo nháp/duyệt/đăng/gỡ; nhánh có merge `ba2cba4`. |

Đại đóng góp việc tích hợp những nhánh này. Tác giả commit tính năng gốc vẫn là các thành viên trong bảng.

## 11. Danh sách commit của Đại và trạng thái đã push

| Commit | Ngày | Nội dung |
| --- | --- | --- |
| `5f5b1dd` | 03/11/2026 | Thêm ứng dụng quản lý cư dân theo kiến trúc controller. |
| `b5ca228` | 13/11/2026 | Merge phần của Luan. |
| `3c7b553` | 13/11/2026 | Merge góp ý của Nam. |
| `91686d6` | 13/11/2026 | Chạy nâng cấp bổ sung khi chạy lại script DB. |
| `69eb164` | 13/11/2026 | Merge nhánh quản lý thông báo. |
| `eee9680` | 13/11/2026 | Thêm runner test quản lý thông báo. |
| `481b12e` | 13/11/2026 | Lưu access token vào localStorage. |

Ở lần đối chiếu remote ngay trước khi lập tài liệu, `main` trên GitHub tại commit `481b12e755ed5534c3a7bc43b40d847e58edee7b`, trùng với main local. Toàn bộ các commit trong bảng đã có trên origin/main. Tài liệu mới này không nằm trong lần xác nhận push đó.

## 12. Đoạn mô tả có thể dùng trong báo cáo

> Em phụ trách nền tảng ứng dụng ASP.NET Core MVC và cơ sở dữ liệu; xây dựng xác thực cookie/JWT, quản lý phiên và refresh token; quản trị tài khoản, vai trò, quyền và gán quyền theo phạm vi; đăng ký và duyệt tài khoản ở phiên bản đầu; giao diện quản trị và client gọi API. Em xây dựng nền tảng SignalR có xác thực cùng trang kiểm tra kết nối, tích hợp mã nguồn của các thành viên, cập nhật script nâng cấp database và bổ sung công cụ chạy kiểm thử quản lý thông báo. Các nghiệp vụ portal cư dân, góp ý và quản lý thông báo được phát triển thêm bởi thành viên khác và được em tích hợp vào main.

## 13. Luồng chức năng và cách code hoạt động

Luồng chính của Đại: **đăng ký tài khoản → admin duyệt → đăng nhập → kiểm tra quyền → sử dụng chức năng quản trị**. Phần giải thích dưới đây dựa trên phiên bản ban đầu của Đại; các nhánh tích hợp có mở rộng một số bước.

### Thay đổi mới: đơn giản hóa màn duyệt đăng ký

Sau phần mô tả lịch sử ở tài liệu này, màn duyệt được đơn giản hóa theo yêu cầu của Đại: hệ thống tìm hồ sơ cư dân chưa liên kết theo CCCD đăng ký, hiển thị để admin đối chiếu, rồi admin xác nhận và chọn Duyệt hoặc Từ chối. Backend tự tra lại CCCD, liên kết hồ sơ và cấp quyền trong transaction; vẫn kiểm tra quyền, RowVersion, hồ sơ đã liên kết và duyệt lặp. Bỏ chọn thủ công toàn bộ danh sách cư dân và bỏ tạo hồ sơ/hộ ngay trong bước duyệt. Nếu thiếu CCCD hoặc chưa có hồ sơ phù hợp, không cho duyệt và hướng dẫn bổ sung/nhờ cán bộ tạo hồ sơ trước. Màn nhập hồ sơ riêng cho cán bộ chưa được triển khai. Các mô tả dưới đây về bản đầu và phần Luan bổ sung là thông tin lịch sử, không phải toàn bộ giao diện hiện tại.

### 13.1. Người dùng đăng ký tài khoản

```text
Views/Account/Register.cshtml
→ AccountController.Register(RegisterInput)
→ RegisterCore(input)
→ lưu AspNetUsers với VerificationStatus = PENDING
→ chuyển về trang đăng nhập, thông báo chờ duyệt
```

`Register()` kiểm tra ModelState và antiforgery. `RegisterCore()` chuẩn hóa tên đăng nhập, kiểm tra trùng, tạo tài khoản, băm mật khẩu và lưu bằng EF Core. Tài khoản tự đăng ký chưa được cấp vai trò hoặc liên kết hồ sơ cư dân.

Đoạn minh họa rút gọn từ logic thực tế, không phải toàn bộ hàm:

```csharp
var user = new AspNetUser
{
    Id = Guid.NewGuid().ToString(),
    UserName = input.UserName.Trim(),
    NormalizedUserName = input.UserName.Trim().ToUpperInvariant(),
    VerificationStatus = "PENDING",
    IsActive = true
};

user.PasswordHash = HashPassword(user, input.Password);
db.AspNetUsers.Add(user);
await db.SaveChangesAsync();
```

Code đầy đủ còn lưu họ tên, số điện thoại, thời gian tạo và metadata của tài khoản. Mật khẩu được lưu dạng hash.

### 13.2. Admin duyệt đăng ký

```text
RegistrationsController.Index()
→ lấy danh sách tài khoản theo trạng thái, mặc định PENDING
→ admin chọn hồ sơ cư dân và duyệt hoặc từ chối
→ Review(input)
→ ReviewRegistration(actor, input)
→ cập nhật tài khoản, vai trò và lịch sử
```

Trong phiên bản ban đầu, Đại đã xử lý:

1. `RequireReviewer()` kiểm tra quyền `accounts.read`, `accounts.update` và vai trò admin có hiệu lực.
2. Không cho tự duyệt tài khoản của mình.
3. Kiểm tra tài khoản tồn tại, đang PENDING, còn hoạt động và RowVersion khớp.
4. Khi duyệt, chọn **hồ sơ cư dân có sẵn**, kiểm tra hồ sơ chưa liên kết tài khoản khác, gán ResidentId và chuyển sang APPROVED.
5. Cấp vai trò ACCOUNT và RESIDENT với phạm vi SELF nếu chưa được gán hợp lệ.
6. Khi từ chối, chuyển sang REJECTED và lưu lý do.
7. Lưu người duyệt, thời gian, tăng PermissionVersion và ghi audit.

Các thay đổi nằm trong transaction để cập nhật đồng bộ:

```csharp
await using var transaction =
    await db.Database.BeginTransactionAsync(
        IsolationLevel.Serializable);

// Kiểm tra dữ liệu, cập nhật tài khoản, gán vai trò và ghi audit.

await db.SaveChangesAsync();
await transaction.CommitAsync();
```

Nếu một bước lỗi trước khi commit, transaction không được hoàn tất. Phần khai báo hồ sơ cư dân chi tiết và tạo hồ sơ cư dân mới khi duyệt được Luan mở rộng sau.

### 13.3. Đăng nhập và tạo phiên

```text
Views/Account/Login.cshtml
→ AccountController.Login(input, returnUrl)
→ AuthControllerBase.LoginCore(input, device)
→ kiểm tra tài khoản, mật khẩu và trạng thái duyệt
→ tạo UserSession
→ CreateTokensCore(): tạo token và refresh cookie
→ SignInCore(): tạo cookie đăng nhập MVC
→ chuyển đến trang phù hợp
```

`LoginCore()` kiểm tra tài khoản tồn tại, IsActive, thời hạn khóa và mật khẩu. Nhập sai 5 lần dẫn tới khóa tạm 15 phút. Tài khoản PENDING hoặc REJECTED chưa được đăng nhập. Khi hợp lệ, hàm tạo phiên có thời hạn tuyệt đối và trả về tài khoản cùng phiên.

Đoạn gọi trong luồng đăng nhập MVC:

```csharp
var result = await LoginCore(
    input, Request.Headers.UserAgent.ToString());

await CreateTokensCore(HttpContext, result.Session);
await SignInCore(HttpContext, result.User, result.Session);
```

`SignInCore()` tạo các claim định danh người dùng, tên đăng nhập và mã phiên `sid`, rồi gọi SignInAsync với scheme Cookies. Bản đầu chuyển người có quyền `accounts.read` ở SYSTEM vào `/Admin`, người khác vào `/Account/Profile`, hoặc sử dụng returnUrl nội bộ hợp lệ. Điều hướng cư dân/cán bộ được mở rộng trong các phần tích hợp sau.

### 13.4. Quản trị tài khoản, vai trò và quyền

```text
Giao diện quản trị
→ JavaScript gọi API
→ xác thực người dùng
→ controller kiểm tra quyền
→ truy vấn/cập nhật bằng MyStockContext
→ SaveChangesAsync() lưu SQL Server
→ trả kết quả cho giao diện
```

| Chức năng | Controller xử lý |
| --- | --- |
| Danh sách, tạo và cập nhật tài khoản | UsersController |
| Danh sách, tạo/cập nhật vai trò, cấu hình quyền | RolesController |
| Quản lý danh mục quyền và phạm vi cho phép | PermissionsController |
| Gán và thu hồi vai trò theo phạm vi/thời gian | RoleAssignmentsController |
| Thông tin và quyền tài khoản hiện tại | MeController |

Các controller dùng logic dùng chung trong `BaseController`. Ví dụ:

```csharp
await RequirePermission(User, "accounts.update");
```

`RequirePermission()` tra quyền hiệu lực trong `VwEffectivePermissions` với phạm vi SYSTEM; thiếu quyền sẽ phát sinh AppFault 403. Kiểm tra nghiệp vụ theo nhóm/khu vực dùng `CanAccessGroup()` để ràng buộc quyền và phạm vi trên cùng assignment. `ProtectLastAdmin()` bảo vệ admin cuối cùng; `SetVersion()` đặt RowVersion gốc để EF Core kiểm tra cập nhật đồng thời; `Audit()` thêm bản ghi lịch sử.

### 13.5. Xác thực API và làm mới token

```text
auth-client.js
→ gửi Authorization: Bearer <access token>
→ BearerHandler xác thực token
→ controller kiểm tra quyền và xử lý
```

Các endpoint xác thực chính ở `AuthController`:

| Endpoint | Xử lý |
| --- | --- |
| GET /api/auth/csrf | Cấp CSRF token phục vụ các request tương ứng. |
| POST /api/auth/login | Gọi LoginCore và CreateTokensCore, trả access token. |
| POST /api/auth/refresh | Gọi RefreshCore để đổi refresh token và trả access token mới. |
| POST /api/auth/logout | Gọi LogoutCore. |
| POST /api/auth/change-password | Gọi ChangePasswordCore. |

Luồng refresh:

```text
auth-client.js cần token mới
→ POST /api/auth/refresh kèm refresh cookie và CSRF token
→ AuthController.Refresh()
→ AuthControllerBase.RefreshCore()
→ kiểm tra refresh token/phiên, thực hiện rotation
→ trả access token mới
→ client tiếp tục gọi API trong giới hạn retry
```

Refresh token được giữ trong cookie HttpOnly; backend lưu hash của token. Commit `481b12e` bổ sung lưu access token và thời hạn vào localStorage với khóa `qlcd.accessToken`, nạp lại khi cần và xóa khi đăng xuất/đổi mật khẩu/xử lý hết phiên.

### 13.6. Kết nối SignalR bản đầu

Luồng dưới đây phục vụ giải thích đóng góp lịch sử của Đại tại commit nền; không dùng `/SignalR` hoặc `Ping()` làm bước demo cho phiên bản hiện tại.

```text
Người đã đăng nhập mở /SignalR
→ notification-client.js kết nối /hubs/notifications bằng cookie
→ nhấn Gửi thử
→ gọi NotificationHub.Ping()
→ kiểm tra phiên bằng BaseController.ValidSession()
→ Clients.Caller.SendAsync("ReceiveNotification", ...)
→ client nhận sự kiện và thêm thông báo lên trang
```

Đại làm Hub có xác thực, cấu hình endpoint, client dùng chung, tự kết nối lại và giao diện thử. Ping chỉ gửi phản hồi cho người gọi. Phần chia nhóm cán bộ/cư dân và sự kiện cập nhật góp ý do Nam bổ sung sau.

### 13.7. Cách giải thích kiến trúc khi báo cáo

Ứng dụng sử dụng ASP.NET Core MVC và Entity Framework Core với SQL Server. Controller xử lý nghiệp vụ trực tiếp qua MyStockContext, không có service layer riêng. View/JavaScript đảm nhiệm giao diện; BaseController chia sẻ kiểm tra quyền/phiên và các tiện ích; AuthControllerBase chia sẻ logic xác thực giữa MVC và API. Cookie dùng cho MVC và SignalR, Bearer JWT dùng cho API. Các thao tác quan trọng kiểm tra quyền ở server, validation, phiên bản dữ liệu và transaction khi cần.

> Luồng em phụ trách là quản lý vòng đời tài khoản: đăng ký, xét duyệt, đăng nhập, quản lý phiên và phân quyền. Em xây dựng các API quản trị tài khoản, vai trò và quyền, giao diện quản trị và client gọi API. Em cũng làm nền tảng SignalR có xác thực và trang kiểm tra kết nối. Các phần nghiệp vụ cư dân, góp ý và quản lý thông báo được nhóm phát triển thêm trên nền này.

## 14. Danh mục file bản đầu để tra cứu

Danh mục dưới đây lấy trực tiếp từ cây Git của commit `5f5b1dd`, gồm mã ứng dụng chính, script SQL, cấu hình và test dạng văn bản. Đã loại thư viện frontend, binary build và mã dự án mẫu `SignalRChat`. File scaffold/model sinh tự động trong danh mục chỉ ghi nhận được đưa vào repo, không khẳng định được viết thủ công.

```text
.editorconfig
.github/copilot-instructions.md
.gitignore
.validation/auth-client.test.cjs
.validation/auth-flow-smoke.cjs
.validation/controllers-smoke.cjs
QuanLyCuDan.sln
QuanLyCuDan/Auth/AuthContracts.cs
QuanLyCuDan/Auth/BearerHandler.cs
QuanLyCuDan/Auth/RegisterInput.cs
QuanLyCuDan/Auth/TokenIssuer.cs
QuanLyCuDan/Controllers/AccountController.cs
QuanLyCuDan/Controllers/AdminController.cs
QuanLyCuDan/Controllers/AuthController.cs
QuanLyCuDan/Controllers/AuthControllerBase.cs
QuanLyCuDan/Controllers/BaseController.cs
QuanLyCuDan/Controllers/HomeController.cs
QuanLyCuDan/Controllers/ManagementController.cs
QuanLyCuDan/Controllers/MeController.cs
QuanLyCuDan/Controllers/PermissionsController.cs
QuanLyCuDan/Controllers/RegistrationsController.cs
QuanLyCuDan/Controllers/RoleAssignmentsController.cs
QuanLyCuDan/Controllers/RolesController.cs
QuanLyCuDan/Controllers/SignalRController.cs
QuanLyCuDan/Controllers/UsersController.cs
QuanLyCuDan/Hubs/NotificationHub.cs
QuanLyCuDan/Models/Activity.cs
QuanLyCuDan/Models/Announcement.cs
QuanLyCuDan/Models/AspNetRole.cs
QuanLyCuDan/Models/AspNetRoleClaim.cs
QuanLyCuDan/Models/AspNetUser.cs
QuanLyCuDan/Models/AspNetUserClaim.cs
QuanLyCuDan/Models/AspNetUserLogin.cs
QuanLyCuDan/Models/AspNetUserToken.cs
QuanLyCuDan/Models/ErrorViewModel.cs
QuanLyCuDan/Models/FeeCampaign.cs
QuanLyCuDan/Models/Household.cs
QuanLyCuDan/Models/ManagementModule.cs
QuanLyCuDan/Models/MyStockContext.cs
QuanLyCuDan/Models/PasswordResetChallenge.cs
QuanLyCuDan/Models/Payment.cs
QuanLyCuDan/Models/Permission.cs
QuanLyCuDan/Models/PermissionAllowedScope.cs
QuanLyCuDan/Models/RefreshToken.cs
QuanLyCuDan/Models/RegistrationListViewModel.cs
QuanLyCuDan/Models/Request.cs
QuanLyCuDan/Models/Resident.cs
QuanLyCuDan/Models/ResidentialUnit.cs
QuanLyCuDan/Models/SchemaVersion.cs
QuanLyCuDan/Models/UserRoleAssignment.cs
QuanLyCuDan/Models/UserSession.cs
QuanLyCuDan/Models/VwEffectivePermission.cs
QuanLyCuDan/Models/VwFeeBalance.cs
QuanLyCuDan/Models/VwPopulationChange.cs
QuanLyCuDan/Program.cs
QuanLyCuDan/Properties/launchSettings.json
QuanLyCuDan/QuanLyCuDan.csproj
QuanLyCuDan/Views/Account/Login.cshtml
QuanLyCuDan/Views/Account/Profile.cshtml
QuanLyCuDan/Views/Account/Register.cshtml
QuanLyCuDan/Views/Admin/Index.cshtml
QuanLyCuDan/Views/Home/Index.cshtml
QuanLyCuDan/Views/Home/Privacy.cshtml
QuanLyCuDan/Views/Management/Index.cshtml
QuanLyCuDan/Views/Registrations/Index.cshtml
QuanLyCuDan/Views/Shared/Error.cshtml
QuanLyCuDan/Views/Shared/_AccountLayout.cshtml
QuanLyCuDan/Views/Shared/_Layout.cshtml
QuanLyCuDan/Views/Shared/_Layout.cshtml.css
QuanLyCuDan/Views/Shared/_ManagementLayout.cshtml
QuanLyCuDan/Views/Shared/_ValidationScriptsPartial.cshtml
QuanLyCuDan/Views/SignalR/Index.cshtml
QuanLyCuDan/Views/_ViewImports.cshtml
QuanLyCuDan/Views/_ViewStart.cshtml
QuanLyCuDan/appsettings.json
QuanLyCuDan/wwwroot/css/admin.css
QuanLyCuDan/wwwroot/css/site.css
QuanLyCuDan/wwwroot/js/account-display.js
QuanLyCuDan/wwwroot/js/action-dialog.js
QuanLyCuDan/wwwroot/js/admin.js
QuanLyCuDan/wwwroot/js/auth-client.js
QuanLyCuDan/wwwroot/js/notification-client.js
QuanLyCuDan/wwwroot/js/password-dialog.js
QuanLyCuDan/wwwroot/js/profile.js
QuanLyCuDan/wwwroot/js/registrations.js
QuanLyCuDan/wwwroot/js/site.js
README.md
```

## 15. Giải thích chi tiết Authentication của Đại

Phần này giải thích cách triển khai đang có để phục vụ đọc code và thuyết trình. Nền xác thực được Đại đưa vào; thay đổi lưu access token trong localStorage thuộc commit `481b12e`. Những phần đăng ký/hồ sơ mở rộng của thành viên khác vẫn được phân biệt như các mục trên.

### 15.1. Xác thực và phân quyền khác nhau thế nào?

**Authentication — xác thực** trả lời “request này của ai, phiên đăng nhập có còn hợp lệ không?”. Hệ thống lấy thông tin từ cookie hoặc JWT, kiểm tra tính hợp lệ rồi tạo `ClaimsPrincipal`, được truy cập qua `User` trong controller.

**Authorization — phân quyền** trả lời “người đó có được làm thao tác này với dữ liệu này không?”. Sau xác thực, controller tiếp tục kiểm tra quyền hiệu lực, địa bàn và quyền sở hữu. Đăng nhập thành công không đồng nghĩa được xem mọi hộ hoặc mọi góp ý.

Ví dụ: tài khoản cán bộ có phiên hợp lệ nhưng chỉ có `feedback.read` ở GROUP A. Khi truy cập góp ý ở GROUP B, hệ thống vẫn phải từ chối. Chỉ ẩn nút trên giao diện không đủ; kiểm tra quyết định nằm ở server.

### 15.2. Ba loại thông tin đăng nhập và nơi lưu

| Thành phần | Nơi lưu / cách gửi | Mục đích và thời hạn trong code |
| --- | --- | --- |
| Cookie MVC `qlcd.auth` | Trình duyệt tự gửi cho website; `HttpOnly`, `SameSite=Strict` | Xác thực trang MVC và kết nối SignalR. Ticket có hạn theo phiên; `IsPersistent=false` nên cookie không được cấu hình là cookie lưu bền qua lần đóng trình duyệt. |
| Access token JWT | `localStorage["qlcd.accessToken"]`, JSON chứa `accessToken` và `expiresAt`; JavaScript đưa vào header `Authorization: Bearer ...` | Xác thực API. Hạn tối đa 10 phút và không vượt hạn tuyệt đối của phiên. |
| Refresh token | Cookie `qlcd.refresh`, `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`; database lưu `TokenHash` | Đổi lấy access token mới. Mỗi token tối đa 7 ngày và không vượt hạn phiên 30 ngày. |

Cookie auth dùng cơ chế bảo vệ ticket của ASP.NET Core Data Protection. JWT được ký bằng khóa của `TokenIssuer`. Đây là hai cơ chế riêng; không dùng JWT trong localStorage làm cookie đăng nhập MVC. Refresh token là chuỗi ngẫu nhiên, không phải JWT.

Ở môi trường ngoài Development, cookie được yêu cầu `Secure`; Development cho phép đi theo HTTP/HTTPS đang dùng. `HttpOnly` ngăn JavaScript đọc cookie. Access token trong localStorage thì JavaScript cùng origin đọc được, kể cả khi có mã XSS chạy trong trang; cách lưu này không có thuộc tính HttpOnly.

### 15.3. Luồng đăng nhập, từng bước

1. MVC nhận form tại `AccountController.Login`; API nhận JSON tại `POST /api/auth/login`. Hai đường cùng dùng `AuthControllerBase.LoginCore()`.
2. Chuẩn hóa tên đăng nhập và tìm tài khoản theo `NormalizedUserName`; kiểm tra tồn tại, `IsActive` và `LockoutEnd`.
3. `VerifyPassword()` so sánh mật khẩu nhập với hash. Sai mật khẩu tăng `AccessFailedCount`; đủ 5 lần thì khóa 15 phút và đặt bộ đếm về 0.
4. Chỉ sau khi mật khẩu đúng mới trả thông tin tài khoản đang PENDING hay REJECTED. Các trạng thái này chưa được đăng nhập.
5. Nếu dữ liệu demo đang dùng định dạng `SHA256$...`, mật khẩu đúng được băm lại bằng `PasswordHasher<AspNetUser>` của Identity. Mật khẩu mới dùng định dạng Identity/PBKDF2, không lưu plaintext.
6. Tạo `UserSession` gồm `Id`, `UserId`, nhãn thiết bị, thời điểm tạo và `AbsoluteExpiresAt = now + 30 ngày`. Các cập nhật của `LoginCore()` được thực hiện trong transaction Serializable.
7. `CreateTokensCore()` tạo bản ghi refresh token, đặt cookie refresh và trả `TokenResponse` chứa JWT, `expiresIn`, `tokenType`.
8. Riêng đường MVC còn gọi `SignInCore()` để tạo cookie `qlcd.auth`, sau đó chuyển trang. Đường API login chỉ trả token và đặt refresh cookie; nó không gọi `SignInCore()` để tạo cookie MVC.

Một điểm cần giải thích khi đọc code: MVC gọi `CreateTokensCore()` để có refresh cookie nhưng không nhúng access token trả về vào HTML. Khi JavaScript gọi API lần đầu mà localStorage chưa có token, `auth-client.js` dùng refresh cookie để lấy JWT rồi lưu. Vì vậy không có endpoint `/api/auth/token`.

### 15.4. JWT chứa gì và được kiểm tra ra sao?

`Auth/TokenIssuer.cs` tạo JWT ký HMAC-SHA256 với issuer `QuanLyCuDan` và audience `QuanLyCuDan.Api`.

| Claim | Ý nghĩa |
| --- | --- |
| `sub` | Id tài khoản; handler JWT ánh xạ định danh để code lấy `ClaimTypes.NameIdentifier`. |
| `sid` | Id bản ghi `UserSession`; liên kết token với phiên trong database. |
| `jti` | Mã riêng cho lần phát hành token. |
| `unique_name` | Tên đăng nhập. |
| `iat`, `nbf`, `exp` | Thời điểm phát hành, bắt đầu hiệu lực và hết hạn. |

Payload JWT có thể đọc được sau khi decode; chữ ký bảo vệ tính toàn vẹn, không mã hóa nội dung. Không đặt mật khẩu hay dữ liệu bí mật vào payload. Token hiện tại cũng không chứa toàn bộ quyền nghiệp vụ; controller đọc quyền hiệu lực từ database.

`BearerHandler.HandleAuthenticateAsync()` lấy token sau tiền tố `Bearer `, gọi `TokenIssuer.Validate()` để kiểm tra chữ ký, thuật toán, issuer, audience và thời hạn. `ClockSkew=0` nghĩa là không cộng thêm khoảng dung sai thời gian. Tiếp theo handler gọi `BaseController.ValidSession()`. Token có chữ ký đúng vẫn bị từ chối nếu phiên đã bị thu hồi.

Khóa ký lấy từ cấu hình `Auth:SigningKey`, là base64 của ít nhất 32 byte. Nếu không cấu hình, `TokenIssuer` tạo khóa ngẫu nhiên sống theo process vì được đăng ký singleton. Khi process khởi động lại, JWT cũ không còn xác minh được bằng khóa ngẫu nhiên mới; client có thể refresh để lấy token mới nếu refresh token và phiên vẫn hợp lệ. Khóa Data Protection của cookie là cơ chế khác; Development lưu tại `.local/keys` trong thư mục ứng dụng.

### 15.5. Vì sao có JWT rồi vẫn kiểm tra database?

`ValidSession()` kiểm tra đồng thời:

- Có user id và claim `sid` là GUID hợp lệ.
- Phiên thuộc đúng user, chưa có `RevokedAt`, chưa qua `AbsoluteExpiresAt`.
- Tài khoản đang hoạt động, không PENDING/REJECTED và không còn trong thời gian khóa.

Cookie xác thực gọi kiểm tra này qua `OnValidatePrincipal`; Bearer handler cũng gọi cùng hàm. Nhờ vậy đăng xuất hoặc đổi mật khẩu có thể vô hiệu hóa request tiếp theo dù JWT còn hạn. Cách triển khai này có trạng thái phiên trong database, không phải xác thực JWT hoàn toàn không tra cứu server.

Sau bước xác thực, `RequirePermission()` kiểm tra quyền SYSTEM cho các API quản trị. `CanAccessGroup()` kiểm tra quyền cùng phạm vi GROUP/AREA trên cùng bản ghi quyền hiệu lực, tránh ghép quyền của địa bàn này với phạm vi của địa bàn khác.

### 15.6. Refresh token rotation và phát hiện dùng lại

`AddRefresh()` tạo 32 byte ngẫu nhiên rồi chuyển sang chuỗi hex. Server gửi chuỗi gốc trong cookie, chỉ lưu SHA-256 vào `RefreshTokens.TokenHash`. Khi refresh, backend băm chuỗi nhận được rồi tìm bản ghi tương ứng; không cần lưu chuỗi token gốc trong DB.

```text
Client POST /api/auth/refresh
  → cookie refresh + header X-CSRF-TOKEN
  → hash token, tìm RefreshTokens và UserSession
  → kiểm tra hạn, thu hồi, tài khoản và ConsumedAt
  → đánh dấu token cũ đã dùng
  → tạo token mới, liên kết ReplacedByTokenId
  → commit transaction
  → trả JWT mới và Set-Cookie refresh mới
```

Nếu token cũ đã có `ConsumedAt` mà tiếp tục được gửi, `RefreshCore()` thu hồi phiên với lý do `REFRESH_REUSE`, lưu DB rồi trả 401. Đây là lý do client không tự gửi lại request refresh khi mất phản hồi: server có thể đã đổi token thành công trước khi đường truyền bị ngắt.

Refresh không kéo dài mốc tuyệt đối 30 ngày của phiên. Token mới chỉ được sống tối đa 7 ngày kể từ lần tạo và luôn bị chặn bởi mốc tuyệt đối đó.

### 15.7. `auth-client.js` xử lý token trong trình duyệt

1. Mỗi lần cần JWT, `loadToken()` đọc JSON từ localStorage. Dữ liệu hỏng được xóa; dữ liệu đúng chỉ được dùng khi chưa qua `expiresAt`.
2. Sau refresh, client lưu hạn sử dụng bằng `Date.now() + (expiresIn - 30) * 1000`, chặn dưới ở 0. Khoảng 30 giây giúp làm mới sớm; server vẫn là nơi quyết định token có hợp lệ không.
3. Nhiều API đồng thời trong một tab dùng chung Promise `pending`, tránh đồng loạt refresh. Khi trình duyệt có Web Locks, khóa `qlcd-refresh` tuần tự hóa các lần refresh giữa các tab cùng origin. Nếu không có Web Locks, code chỉ còn gộp Promise trong từng tab.
4. Client lấy CSRF token mới trước mỗi lần refresh, rồi gửi refresh cookie bằng `credentials: "same-origin"`.
5. API trả 401 thì làm mới và thử lại request API tối đa một lần. Vẫn 401 thì xóa token và chuyển tới `/Account/Login`. API trả 403 được báo lỗi quyền, không tự refresh hay lặp lại thao tác ghi.
6. Sau logout/đổi mật khẩu qua `authApi`, token được xóa. Form logout của các layout cũng xóa localStorage; trang Login xóa token cũ để tránh dùng lại token của tài khoản trước.

Việc sửa nội dung hoặc `expiresAt` trong localStorage không giúp vượt xác thực: server vẫn kiểm tra chữ ký, thời hạn thật của JWT và phiên DB. Xóa localStorage chỉ xóa bản lưu ở trình duyệt; thao tác thu hồi phiên thực sự nằm ở backend.

### 15.8. Logout, đổi mật khẩu, CSRF và mã lỗi

`LogoutCore()` thu hồi **phiên hiện tại** theo user id + `sid`, ghi lý do `LOGOUT`, xóa refresh cookie đúng path và gọi `SignOutAsync("Cookies")`. `ChangePasswordCore()` kiểm tra mật khẩu hiện tại, băm mật khẩu mới, đổi `SecurityStamp` và thu hồi **mọi phiên chưa bị thu hồi** của user với lý do `PASSWORD_CHANGED` trong transaction. Request sau đó thất bại nhờ kiểm tra phiên, không cần chờ JWT hết hạn.

Các request dùng cookie được trình duyệt tự gửi nên cần kiểm soát CSRF. API login/refresh dùng `[ValidateAntiForgeryToken]`; client lấy token tại `GET /api/auth/csrf` và gửi header `X-CSRF-TOKEN`. Form MVC dùng antiforgery token. API logout/đổi mật khẩu yêu cầu Bearer; code không gắn `[ValidateAntiForgeryToken]` cho hai action này. `Program.cs` còn kiểm tra Origin khi header có mặt cho request không phải GET vào `/api/auth` và `/Account`.

| Mã | Cách hiểu trong luồng này |
| --- | --- |
| 400 | Dữ liệu không hợp lệ, Origin không phù hợp hoặc lỗi antiforgery tùy điểm kiểm tra. |
| 401 | Chưa xác thực, token sai/hết hạn hoặc phiên không còn hiệu lực. |
| 403 | Tài khoản chưa được duyệt hoặc người dùng không có quyền theo điều kiện xử lý. |
| 409 | Xung đột dữ liệu, phiên bản hoặc ràng buộc nghiệp vụ tương ứng. |
| 429 | Vượt giới hạn tần suất; login 10 request/phút/IP, refresh 30 request/phút/IP. |

### 15.9. Cách trình bày ngắn khi bảo vệ

> Em dùng cookie cho trang MVC và SignalR, JWT Bearer cho API. Cả cookie và JWT đều có mã phiên sid; server kiểm tra phiên trong database để thu hồi được ngay ở request tiếp theo. Access token sống tối đa 10 phút, được lưu localStorage theo cập nhật mới. Refresh token nằm trong cookie HttpOnly, DB chỉ giữ hash; mỗi lần refresh sẽ đổi token, dùng lại token cũ thì thu hồi phiên. Sau xác thực, controller còn kiểm tra quyền và phạm vi, nên đăng nhập được chưa có nghĩa được truy cập mọi dữ liệu.

## 16. Giải thích chi tiết SignalR và phần Đại đóng góp

### 16.1. SignalR giải quyết việc gì?

HTTP thông thường bắt đầu khi trình duyệt gửi request rồi server trả response. Với cập nhật góp ý, nếu chỉ dùng cách đó thì người nhận phải tải lại hoặc liên tục hỏi server. SignalR quản lý kết nối để server có thể chủ động phát sự kiện tới client đang kết nối.

Trong code, `HubConnectionBuilder().withUrl("/hubs/notifications")` tạo client; `NotificationHub` là điểm kết nối phía server. Không ép transport trong cấu hình, nên không khẳng định mọi kết nối đều dùng WebSocket: SignalR thương lượng transport phù hợp. `SendAsync` gửi sự kiện, không thay thế việc lưu dữ liệu bằng EF Core.

### 16.2. Nền tảng Đại làm ở commit gốc

| File / cấu hình bản nền | Vai trò |
| --- | --- |
| `Program.cs`: `AddSignalR()` | Đăng ký dịch vụ SignalR để DI và Hub hoạt động. |
| `MapHub<NotificationHub>("/hubs/notifications", ...)` | Ánh xạ URL kết nối, bật đóng kết nối khi ticket xác thực hết hạn. |
| `NotificationHub` với `[Authorize(AuthenticationSchemes = "Cookies")]` | Chỉ cho người xác thực bằng cookie kết nối. |
| `Ping()` ở commit `5f5b1dd` | Kiểm tra phiên lại, ngắt kết nối nếu phiên sai; gửi `ReceiveNotification` về `Clients.Caller` nếu hợp lệ. |
| `SignalRController` và view thử bản nền | Cho người đã đăng nhập kiểm tra trạng thái và gửi thử. |
| `notification-client.js` | Đóng gói kết nối, callback nhận tin, trạng thái và tự kết nối lại. |

`Clients.Caller` chỉ là kết nối đang gọi hàm Hub. Nó không gửi tới tất cả người dùng. Kiểm tra phiên ngay trong `Ping()` bản đầu giúp xử lý trường hợp kết nối còn mở nhưng phiên đã bị đăng xuất ở tab khác.

Sau tích hợp của Nam, Hub không còn phương thức `Ping()`, controller và view thử cũng không còn. Vì vậy nếu giải thích đóng góp của Đại thì dùng commit nền làm bằng chứng; nếu demo code hiện tại thì dùng `/Feedback`.

### 16.3. Authentication của kết nối SignalR hiện tại

```text
Đăng nhập MVC → có cookie qlcd.auth
  → mở trang /Feedback, tải thư viện SignalR và client dùng chung
  → notificationClient.start()
  → kết nối /hubs/notifications, trình duyệt gửi cookie
  → middleware xác thực cookie và [Authorize] kiểm tra
  → NotificationHub.OnConnectedAsync() kiểm tra ValidSession
  → server gán ConnectionId vào nhóm được phép nhận sự kiện
```

SignalR trong dự án này không lấy JWT từ localStorage, không cấu hình `accessTokenFactory`. Chỉ đăng nhập qua API để lấy JWT chưa tạo cookie MVC, vì thế không đủ để kết nối Hub yêu cầu scheme Cookies.

`Context.User` là danh tính đã xác thực của kết nối; `Context.ConnectionId` là mã kết nối, không phải user id. Một người mở nhiều tab có thể có nhiều ConnectionId. Group cho phép tập hợp các kết nối để server gửi đúng nhóm người nhận.

### 16.4. Chia nhóm góp ý sau tích hợp

Logic dưới đây là phần mở rộng của Nam trên nền SignalR của Đại:

1. `OnConnectedAsync()` gọi `ValidSession()`. Sai thì `Context.Abort()` và kết thúc.
2. Lấy user id từ claim định danh, thêm kết nối vào `feedback:user:{userId}`. Mỗi tab hợp lệ của cùng user có thể thuộc nhóm này.
3. Tra `VwEffectivePermissions` với quyền `feedback.read` của user.
4. Quyền GROUP thì thêm vào `feedback:staff:{groupId}` tương ứng.
5. Quyền AREA thì lấy các nhóm đang hoạt động có `ParentUnitId` thuộc khu vực được cấp, rồi thêm vào từng nhóm cán bộ đó.

Client không tự truyền tên group để xin tham gia. Server quyết định group từ danh tính và dữ liệu quyền. Tuy nhiên group chỉ phục vụ định tuyến sự kiện; các action HTTP vẫn kiểm tra quyền riêng khi đọc hoặc ghi góp ý.

### 16.5. Một lần gửi góp ý đi qua những đâu?

```text
Cư dân POST /Feedback/Create
  → FeedbackController kiểm tra quyền và dữ liệu
  → lưu Requests và Activity sự kiện trong database
  → IHubContext<NotificationHub>
      .Clients.Group(FeedbackStaffGroup(groupId))
      .SendAsync("FeedbackChanged", { requestId, message })
  → các trình duyệt cán bộ trong nhóm nhận sự kiện
  → callback onFeedback cập nhật thông báo trên giao diện
```

`IHubContext` cho phép controller phát sự kiện mà không phải tạo instance Hub hay gọi controller khác qua DI. Controller vẫn xử lý nghiệp vụ và lưu DB; Hub/SignalR chuyển thông tin cập nhật tới người đang kết nối.

| Thao tác trong `FeedbackController` | Đối tượng nhận `FeedbackChanged` |
| --- | --- |
| `Create` | Nhóm cán bộ của địa bàn được giao xử lý. |
| `Process` | Nhóm của người gửi góp ý, thông báo đã tiếp nhận. |
| `Reply` | Nhóm của người gửi và nhóm cán bộ địa bàn. |

Payload hiện tại có `requestId` và `message`, không gửi toàn bộ hồ sơ hoặc nội dung trao đổi qua sự kiện. Dữ liệu chi tiết được đọc lại qua trang/controller có kiểm tra quyền.

### 16.6. Client nhận sự kiện và kết nối lại

`notification-client.js` đăng ký handler trước khi `connection.start()`. Module cung cấp `onMessage`, `onFeedback`, `onStatus` để trang gắn callback; biến `connection` tránh tạo thêm kết nối khi đã có một instance trong trang.

- `ReceiveNotification` gọi `messageCallback`; handler này còn được giữ từ nền dùng chung, nhưng Hub hiện tại không có `Ping()` phát sự kiện thử.
- `FeedbackChanged` gọi `feedbackCallback`. Trong `Views/Feedback/Index.cshtml`, callback hiện thông báo tải lại để xem nội dung mới. Nếu sự kiện thuộc góp ý đang mở, trang tự reload sau khoảng 1 giây.
- `.withAutomaticReconnect()` bật cơ chế tự thử kết nối lại sau khi kết nối đã được thiết lập rồi bị gián đoạn. `onreconnecting` và `onreconnected` cập nhật trạng thái hiển thị.
- Nếu lần `start()` đầu tiên thất bại hoặc kết nối đóng hẳn, code đặt `connection=null`, hiện hướng dẫn kiểm tra đăng nhập/tải lại trang. Không có vòng retry vô hạn cho lần start đầu.
- Khi có kết nối mới, `OnConnectedAsync()` chạy và gán nhóm theo quyền tại thời điểm kết nối đó.

### 16.7. Giới hạn cần nói đúng khi báo cáo

**Lưu DB và phát SignalR là hai việc riêng.** Góp ý và tin nhắn được lưu trước khi phát sự kiện. Code hiện tại không triển khai một hàng đợi phát tin bền vững cho `FeedbackChanged`; mất kết nối có thể làm client bỏ lỡ sự kiện, nhưng người dùng có thể tải lại để đọc dữ liệu đã lưu. Auto reconnect không tự phát lại toàn bộ sự kiện đã bỏ lỡ.

**Hết hạn cookie khác với thu hồi phiên trong DB.** `CloseOnAuthenticationExpiration=true` xử lý hết hạn ticket xác thực. Không có cơ chế trong Hub hiện tại chủ động đẩy lệnh ngắt mọi kết nối ngay khi một phiên bị thu hồi hoặc quyền thay đổi trong DB. `OnConnectedAsync()` kiểm tra phiên/quyền lúc kết nối; các thao tác HTTP kiểm tra lại quyền, còn membership group của kết nối đang mở chưa có cơ chế tự cập nhật liên tục. Không khẳng định logout ở tab khác lập tức đóng mọi socket.

**SignalR group không phải bảng dữ liệu.** Membership gắn với kết nối đang sống; lịch sử nghiệp vụ nằm trong Requests/Activities. Module thông báo nhóm có bản ghi notification trong Activities không đồng nghĩa đã dùng cùng một bộ phát SignalR cho mọi thông báo toàn hệ thống.

### 16.8. Cách demo và giải thích đóng góp

1. Dùng hai profile trình duyệt: cư dân đã xác minh và cán bộ có `feedback.read` cùng quyền xử lý/phản hồi đúng GROUP/AREA.
2. Mở `/Feedback` ở cả hai, kiểm tra trạng thái kết nối; tạo góp ý bằng dữ liệu demo.
3. Quan sát cán bộ nhận thông báo cập nhật; tiếp nhận và phản hồi để cư dân thấy thay đổi.
4. Mở đúng góp ý đang xem để minh họa cơ chế tự tải lại; tài khoản khác địa bàn phải được kiểm tra quyền riêng khi truy cập dữ liệu.
5. Có thể xem request negotiate/kết nối Hub trong Network của DevTools; không đưa token/cookie thật vào báo cáo.

> Em xây dựng phần nền SignalR: đăng ký dịch vụ, endpoint Hub có xác thực cookie, kiểm tra phiên khi gửi thử, client dùng chung và cơ chế reconnect. Bản đầu dùng Ping để kiểm tra gửi/nhận với chính người gọi. Sau đó Nam mở rộng Hub để chia nhóm cán bộ/cư dân và phát sự kiện FeedbackChanged từ nghiệp vụ góp ý; em tích hợp phần này vào main. SignalR giúp báo cho giao diện biết dữ liệu đã thay đổi, còn dữ liệu và kiểm tra quyền vẫn nằm ở controller và database.

### 16.9. Các file nên mở khi thuyết trình

| Nội dung cần giải thích | File tra cứu |
| --- | --- |
| Cấu hình cookie, Bearer, CSRF, rate limit, endpoint Hub | `QuanLyCuDan/Program.cs` |
| Đăng nhập, tạo phiên, refresh rotation, logout, đổi mật khẩu | `QuanLyCuDan/Controllers/AuthControllerBase.cs` |
| Endpoint API xác thực | `QuanLyCuDan/Controllers/AuthController.cs` |
| Phát hành / kiểm tra JWT | `QuanLyCuDan/Auth/TokenIssuer.cs`, `BearerHandler.cs` |
| Kiểm tra phiên và quyền | `QuanLyCuDan/Controllers/BaseController.cs` |
| localStorage, làm mới và retry API | `QuanLyCuDan/wwwroot/js/auth-client.js` |
| Nhóm kết nối SignalR hiện tại | `QuanLyCuDan/Hubs/NotificationHub.cs` |
| Khởi tạo client, event và reconnect | `QuanLyCuDan/wwwroot/js/notification-client.js` |
| Phát sự kiện sau thao tác góp ý | `QuanLyCuDan/Controllers/FeedbackController.cs` |
| Hiển thị sự kiện, tự tải lại chi tiết | `QuanLyCuDan/Views/Feedback/Index.cshtml` |
| Hub Ping và trang thử của Đại ở bản gốc | Dùng `git show 5f5b1dd:<đường dẫn file>`; không tìm endpoint thử trong bản hiện tại. |
