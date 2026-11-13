# Phạm vi Trang công khai và Cổng cư dân

Chỉ `HomeController`, các action đăng ký cư dân trong `AccountController` và `ResidentController` sử dụng `PortalScope`.
Không có global query filter, thay đổi schema, dữ liệu seed, phân quyền quản trị hay đăng ký service/middleware toàn hệ thống.

## Chọn địa bàn chính thức

Đã lưu cấu hình **chạy thử local** trong `QuanLyCuDan/appsettings.Development.json` (Git ignored):
`GroupCode=TO-A`, `LocalityType=NEIGHBORHOOD`, kết nối SQL Server `localhost` / database `QuanLyCuDan`.
Tổ A Demo (ID 3) đã được đối chiếu lại đang hoạt động, kể cả AREA cha.
Chạy bình thường bằng Visual Studio hoặc `dotnet run --project QuanLyCuDan --launch-profile http`; không cần đặt biến môi trường mỗi lần.
Đây là địa bàn demo để ứng dụng trên máy chạy được, chưa phải xác nhận tên/loại địa bàn triển khai chính thức.
Code không có ID/mã mặc định hard-code; cấu hình môi trường khác vẫn phải chọn địa bàn hợp lệ.
`appsettings.Portal.example.json` chỉ là mẫu; ASP.NET Core **không tự nạp** file này.
Đặt hai khóa sau qua environment variables, user secrets hoặc gộp riêng section `PortalScope` vào cấu hình local hiện có:

```powershell
$env:PortalScope__GroupCode = '<mã GROUP đã đối chiếu với database>'
$env:PortalScope__LocalityType = 'NEIGHBORHOOD' # hoặc VILLAGE nếu đơn vị là thôn
dotnet run --project QuanLyCuDan --launch-profile http
```

Tên và ID luôn lấy từ bản ghi thực tế trong database, không nhập tên tùy ý. `LocalityType` chỉ xác định nhãn giao diện:
database hiện chỉ có `AREA` và `GROUP`, chưa có cột phân biệt thôn/tổ. Chủ đề tài cần xác nhận loại đơn vị.
Một mã duy nhất áp dụng đồng thời cho hai cổng. Chuyển mã bằng cấu hình, không sửa controller.

Truy vấn chỉ đọc để đối chiếu:

```sql
SELECT g.Id, g.Code, g.Name, g.UnitType, g.IsActive,
       a.Id AS AreaId, a.Name AS AreaName, a.IsActive AS AreaIsActive
FROM dbo.ResidentialUnits g
JOIN dbo.ResidentialUnits a ON a.Id = g.ParentUnitId
WHERE g.UnitType = 'GROUP' AND a.UnitType = 'AREA'
ORDER BY g.Id;
```

Đã đối chiếu ngày 2026-11-13 trên SQL Server `localhost`, database `QuanLyCuDan`:
`TO-A` = ID 3 / Tổ A Demo; `TO-B` = ID 4 / Tổ B Demo; `TO-C` = ID 5 / Tổ C Demo.
Đây là dữ liệu mẫu thực tế, **không phải quyết định chọn địa bàn chính thức**.
SQL Express trên máy này không chứa database `QuanLyCuDan`. Đã sửa **file Development local** từ SQL Express sang `localhost` để khắc phục lỗi chạy lại.
Không sửa connection string trong cấu hình được Git theo dõi hoặc cấu hình của thành viên khác. Có thể override qua environment nếu cần:

```powershell
$env:ConnectionStrings__MyStock = 'Server=localhost;Database=QuanLyCuDan;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
# Cấu hình local đã lưu TO-A; chỉ cần override PortalScope nếu muốn đổi sang địa bàn khác đã đối chiếu.
```

Thiếu mã/loại, loại không hợp lệ, mã không tồn tại, mã trỏ AREA, GROUP ngừng hoạt động hoặc AREA cha ngừng hoạt động:
hai cổng trả **503**, không mở rộng sang địa bàn khác. Kiểm tra lại ở mỗi request và khi gửi yêu cầu.
Đăng nhập và chức năng quản trị không phụ thuộc cấu hình này.

## Quy tắc truy cập

- Công khai: đúng địa bàn + `PUBLIC` + `PUBLISHED` + `APPROVED` + đã tới ngày đăng + chưa hết hạn.
  URL `/thong-bao/{id}` trả 404 khi không đủ điều kiện. Tham số địa bàn cũ không được dùng trong truy vấn.
  Model thông báo hiện không có tệp đính kèm hoặc endpoint tải tệp công khai; không tạo thêm nghiệp vụ này.
- Đăng ký/bổ sung: browser không bind `GroupId`. Server tự gán ID; JSON hồ sơ vẫn giữ `GroupId` đúng hợp đồng duyệt hồ sơ hiện có.
  Giữ validation, hash mật khẩu, `PENDING`, không tạo cư dân/hộ/role trước duyệt. Kiểm tra lại validation cấp model sau khi gán ID.
- Đã có hộ: tài khoản `APPROVED`, liên kết Resident, hộ hiện tại chưa lưu trữ và thuộc địa bàn phục vụ. Mọi action GET/POST được kiểm tra trước khi chạy.
- Có hộ ngoài địa bàn: 403 cho toàn bộ cổng cư dân, kể cả cập nhật liên hệ, gửi yêu cầu và tải tệp trực tiếp.
- Chưa có Resident: không vào `/Resident`; dùng `/Account/ResidentRegistration` nếu thỏa điều kiện bổ sung hồ sơ hiện hành.
- Có Resident nhưng chưa có hộ: chỉ dùng tiếp luồng có sẵn khi trạng thái là `MOVED_OUT`, tài khoản đã duyệt có người/ngày xác minh,
  và hồ sơ đăng ký hợp lệ trỏ đúng địa bàn. Chỉ xem/cập nhật hồ sơ cá nhân và gửi/theo dõi/hủy, bổ sung/tải giấy tờ của yêu cầu `MOVE_IN`/`ADD_MEMBER` của mình.
  Chưa được xem hộ, khoản thu, phản ánh, thông báo cư dân hoặc thông báo cá nhân. Không tự cấp thêm role.
- Thiếu bằng chứng địa bàn đã xác minh: 403, không suy ra tư cách cư dân từ ID gửi lên hoặc từ yêu cầu tự khai.
- Yêu cầu/phản ánh: đúng người gửi, `Source=RESIDENT`, đúng địa bàn; nơi tiếp nhận lấy từ server.
  Hộ nhận phải thuộc địa bàn. Giữ CSRF, rowversion, idempotency, điều kiện hủy và một yêu cầu cư trú đang xử lý tại một thời điểm.
- Khoản thu: đúng hộ, đúng chủ hộ và campaign thuộc địa bàn. Thông báo cá nhân: đúng người nhận; tài nguyên Request/Announcement/Payment được tham chiếu cũng phải đúng phạm vi.

Không có đặc tả tin cậy để một người chưa xác minh hoặc đang có hộ ở tổ khác tự xin tư cách cư dân tại cổng này.
Các trường hợp đó bị từ chối; cần nghiệp vụ riêng được nhóm xác nhận nếu muốn mở rộng. Không sửa phía Ban quản lý xử lý/duyệt.

## Chạy và kiểm thử

Sau khi cấu hình địa bàn, mở `/` và `/Account/Register`; đăng nhập cư dân đúng địa bàn rồi mở `/Resident`.
Thử query `/?groupId=<ID khác>`, URL thông báo ngoài địa bàn, tài khoản thuộc tổ khác và các đường dẫn trực tiếp tới yêu cầu/tệp.

```powershell
dotnet build QuanLyCuDan/QuanLyCuDan.csproj --no-restore
dotnet test .validation/PortalScope.Tests/PortalScope.Tests.csproj
node --test .validation/auth-client.test.cjs
```

Bộ HTTP mới chạy qua ứng dụng thật bằng WebApplicationFactory, cookie, CSRF và Razor, trên SQLite `:memory:` riêng cho từng test.
Mọi mutation đều ở database kiểm thử, không ghi vào SQL Server nhóm. Test adapter thay view quyền bằng bảng fixture, mô phỏng rowversion,
và chuyển default/clock SQL Server cho SQLite. Do đó không coi kết quả này là chứng minh đầy đủ trigger/constraint/view SQL Server.
Không có thay đổi production nhằm chiều theo provider kiểm thử.

Smoke chỉ đọc dữ liệu nghiệp vụ thực tế (cần database demo có thông báo công khai ở hai địa bàn và hai tài khoản đã xác minh):

```powershell
$env:AUTH_TEST_URL = 'http://127.0.0.1:5108' # địa chỉ ứng dụng đang chạy
$env:PORTAL_TEST_SQL_SERVER = 'localhost'
$env:PORTAL_TEST_GROUP_CODE = '<mã đã cấu hình trên ứng dụng>'
$env:PORTAL_TEST_RESIDENT_USER = '<tài khoản thuộc địa bàn đó>'
$env:PORTAL_TEST_OUTSIDE_USER = '<tài khoản thuộc địa bàn khác>'
$env:PORTAL_TEST_PASSWORD = '<mật khẩu demo của hai tài khoản>'
node --test .validation/portal-scope-sql-smoke.cjs
$env:PORTAL_TEST_USER = $env:PORTAL_TEST_RESIDENT_USER
node .validation/portal-browser.cjs
```

Smoke SQL chỉ SELECT qua sqlcmd. Đăng nhập/đăng xuất vẫn ghi session/token/audit theo nghiệp vụ xác thực hiện có.
Chrome headless kiểm tra responsive 390/1440 px và trường điều kiện, ảnh ở `QuanLyCuDan/.local` (ignored).
Hai script legacy `resident-portal-smoke.cjs` và `registration-onboarding-smoke.cjs` đã được điều chỉnh đầu vào địa bàn/form;
chúng có tạo fixture và gọi duyệt phía quản trị nên không chạy trên database nhóm trong lần này.
Nếu chạy trên môi trường demo riêng, cần `PORTAL_TEST_GROUP_CODE`; script resident còn cần `PORTAL_TEST_OTHER_USER` là cư dân không phải chủ hộ cùng địa bàn.

Không commit/push/merge. Thay đổi có sẵn ở `SignalRChat/Program.cs` và `SignalRChat/appsettings.json` được giữ nguyên.

## Kết quả kiểm chứng ngày 2026-11-13

| Kiểm tra đã chạy | Kết quả cuối |
|---|---|
| `dotnet build QuanLyCuDan/QuanLyCuDan.csproj --no-restore --nologo` | Thành công, 0 warning, 0 error |
| HTTP/MVC trên SQLite bộ nhớ: `PortalScope.Tests` | 87 pass, 0 fail, 0 skip |
| HTTP + SQL Server localhost: `portal-scope-sql-smoke.cjs` | 7 pass, 0 fail, 0 skip (gồm test cha và 6 test con) |
| JavaScript xác thực: `auth-client.test.cjs` | 6 pass, 0 fail, 0 skip |
| Chrome headless | Luồng public/đăng ký/cư dân thành công ở 390 và 1440 px; trường điều kiện và kiểm tra tràn ngang đạt |
| Kiểm tra ảnh thủ công | Đã xem ảnh trang công khai mobile, đăng ký mobile, cư dân mobile và desktop |
| `node --check` | Đạt với smoke SQL, browser, resident legacy và onboarding legacy |
| `git diff --check -- QuanLyCuDan .validation` | Không có lỗi whitespace |

Chạy SQL smoke và Chrome trên bản build cuối cùng với `TO-A/NEIGHBORHOOD`, `dan01` trong địa bàn và `dan03` ngoài địa bàn.
Trong lần QA ban đầu, cấu hình chỉ tồn tại trong process và server kiểm thử đã dừng.
Sau phản hồi về lỗi 503 khi chạy lại, đã lưu cấu hình Development local như phần đầu tài liệu; không còn phụ thuộc phiên QA cũ.
Kết quả TRX: `QuanLyCuDan/.local/test-results/portal-scope.trx`; log HTTP suite: `QuanLyCuDan/.local/portal-tests.log`.
Ảnh: `.local/portal-mobile.png`, `registration-mobile.png`, `resident-mobile.png`, `resident-desktop.png` (đều trong project chính).

Giới hạn: chưa chốt địa bàn chính thức và loại đơn vị; chưa chạy toàn bộ luồng duyệt nghiệp vụ quản trị hoặc ghi thử hồ sơ/yêu cầu/liên hệ vào SQL Server nhóm.
Các mutation đó được kiểm thử qua HTTP trên SQLite riêng. Không kiểm thử solution `SignalRChat` vì ngoài phạm vi; chỉ build project chính và project test.
Không tuyên bố kiểm chứng toàn bộ module nhóm hoặc toàn bộ constraint SQL Server bằng SQLite.

## Danh sách file sửa/tạo (21 file)

| File (từ root repository) | Thay đổi |
|---|---|
| `QuanLyCuDan/Controllers/HomeController.cs` | Ràng buộc list/detail/privacy vào địa bàn, giữ tìm kiếm/phân trang |
| `QuanLyCuDan/Controllers/AccountController.cs` | Chỉ thay đăng ký/bổ sung hồ sơ và helper liên quan; không sửa logic Login/Logout/Profile |
| `QuanLyCuDan/Controllers/ResidentController.cs` | Kiểm tra mọi action, phân biệt chưa có hộ, giới hạn tài nguyên và nơi tiếp nhận |
| `QuanLyCuDan/Models/ResidentPortalViewModel.cs` | Bỏ lựa chọn địa bàn từ view/input riêng của hai cổng, thêm cờ onboarding |
| `QuanLyCuDan/Views/Home/Index.cshtml` | Tên địa bàn cố định, bỏ bộ lọc nhiều tổ |
| `QuanLyCuDan/Views/Resident/Index.cshtml` | Tên địa bàn, bỏ dropdown, hạn chế UI chưa có hộ |
| `QuanLyCuDan/Views/Resident/Announcement.cshtml` | Nhãn chung dùng được cho tổ hoặc thôn |
| `QuanLyCuDan/Views/Account/Register.cshtml` | Form không còn lựa chọn nhiều tổ |
| `QuanLyCuDan/Views/Account/ResidentRegistration.cshtml` | Bổ sung hồ sơ vào địa bàn cố định |
| `QuanLyCuDan/Views/Shared/_RegistrationProfileFields.cshtml` | Chỉ được dùng bởi hai form đăng ký; hiển thị tên thay trường GroupId |
| `QuanLyCuDan/Views/Shared/_PortalLayout.cshtml` | Layout riêng hai cổng hiển thị tên/loại địa bàn |
| `QuanLyCuDan/Portal/PortalScope.cs` (mới) | Tra mã cấu hình vào GROUP/AREA thật, trả 503 khi không hợp lệ |
| `QuanLyCuDan/appsettings.Portal.example.json` (mới) | Mẫu khóa cấu hình, để trống địa bàn chưa xác nhận |
| `QuanLyCuDan/Portal/README.md` (mới) | Cấu hình, quy tắc, kiểm thử và báo cáo này |
| `.validation/PortalScope.Tests/PortalScope.Tests.csproj` (mới) | Dependency kiểm thử riêng, không thêm dependency production |
| `.validation/PortalScope.Tests/PortalFactory.cs` (mới) | Host ứng dụng và database kiểm thử tách biệt |
| `.validation/PortalScope.Tests/PortalHttpTests.cs` (mới) | 87 ca HTTP về phạm vi, validation, CSRF, IDOR, sở hữu, onboarding và kiểm tra quản trị không bị scope |
| `.validation/portal-scope-sql-smoke.cjs` (mới) | Smoke SELECT/HTTP trên SQL Server đang có |
| `.validation/portal-browser.cjs` | Chỉ cập nhật nhánh cư dân: không có dropdown và yêu cầu tài khoản đúng địa bàn; giữ nguyên nhánh quản trị |
| `.validation/resident-portal-smoke.cjs` | Fixture dùng mã địa bàn tường minh và tài khoản đối chiếu cùng địa bàn |
| `.validation/registration-onboarding-smoke.cjs` | Assertion form không có GroupId, fixture dùng mã cấu hình tường minh |

Diff không có sửa đổi ở `AnnouncementsController`, `ManagementController`, `RegistrationsController`, `AdminController`,
`BaseController`, `AuthControllerBase`, `MyStockContext`, `Program.cs` project chính, model database, layout/admin assets hoặc các script SQL.
Hai thay đổi `SignalRChat` đã tồn tại trước nhiệm vụ vẫn có numstat như lúc bắt đầu (Program +3/-1, appsettings +7/-7), không thuộc bản sửa này.
