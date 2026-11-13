# Nâng cấp tổ chức và giao diện cổng cộng đồng

## Phạm vi và nguồn dữ liệu

Chỉ thay đổi Trang công khai và Cổng cư dân, dữ liệu đọc liên quan, stylesheet riêng và kiểm thử.
Không có giao diện hoặc endpoint ghi/phê duyệt hội viên, phân công chức danh hay điều hành tổ chức.
Không sửa nghiệp vụ quản trị, quyền quản trị, dữ liệu cư dân/hộ/tài khoản/yêu cầu hiện có hoặc project SignalRChat.

Đã đọc trực tiếp tài liệu nguồn `1 bí thư.docx` bằng OOXML, đối chiếu với danh sách làm rõ của chủ đề tài.
Văn bản gốc có một số lỗi gõ; `SourceText` giữ nguyên dòng nguồn, tên danh mục dùng cách viết trong yêu cầu đã làm rõ.
Không bịa tên người, chức danh đứng đầu, địa chỉ, CCCD, số điện thoại hoặc mô tả nhiệm vụ.
`TO-A` đã đối chiếu trên SQL Server `localhost`, database `QuanLyCuDan`: ID **3**, GROUP đang hoạt động, AREA cha ID 1 đang hoạt động.
Không dùng chuỗi `TO-A` làm khóa ngoại. Script tra mã trong database để lấy bigint ID thực tế.

## Mô hình bổ sung

Database hiện có 23 bảng, không có bảng tổ chức/chức danh/hội viên để tái sử dụng. Bổ sung đúng 4 bảng:

| Bảng | Dữ liệu và quan hệ |
|---|---|
| `PortalOrganizations` | Danh mục tổ chức; `ResidentialGroupId` → `ResidentialUnits.Id`; unique địa bàn + mã, cờ hoạt động/công khai |
| `PortalOrganizationPositions` | Chức danh thuộc tổ chức; min/max nullable, thông tin nguồn và cờ cần xác nhận |
| `PortalOrganizationAssignments` | Chức danh → cư dân thực tế (`Residents.Id`), khoảng thời gian tùy chọn, cờ hoạt động/xác nhận/cho công bố tên |
| `PortalOrganizationMembers` | Tổ chức ↔ cư dân nhiều–nhiều, vai trò nếu có, khoảng thời gian tùy chọn, hoạt động/xác nhận |

Một cư dân có thể thuộc nhiều hội và đảm nhiệm chức danh theo dữ liệu đã xác nhận.
Một cặp tổ chức/cư dân chỉ có một bản ghi hội viên; một cặp chức danh/cư dân có một bản ghi phân công với khoảng thời gian tùy chọn.
Chưa triển khai lịch sử nhiều nhiệm kỳ cho cùng một cặp hoặc nghiệp vụ cập nhật, vì chưa có đặc tả/quyền quản trị trong phạm vi này.
Không có cascade delete từ các bảng hiện hữu. Không đổi khóa hoặc quan hệ cũ.
Min/max là định mức nguồn; không ép hệ thống phải có đủ nhân sự tối thiểu và không suy ra số người đang đảm nhiệm.
Không tạo trigger áp đặt giới hạn số người đang giữ chức qua nhiều nhiệm kỳ khi chưa có quy tắc thời gian/phân công.

## Danh mục theo tài liệu

| Tổ chức | Chức danh và định mức |
|---|---|
| Chi bộ Đảng | Bí thư: 1; Phó Bí thư: 2–3 |
| Ban lãnh đạo thôn | Trưởng thôn: 1; Phó Trưởng thôn: 2–3; giữ tên theo tài liệu và đánh dấu cần xác nhận khi dùng tại tổ dân phố |
| Ban công tác Mặt trận | Trưởng ban: chưa xác định số lượng; Phó Trưởng ban: 2 |
| Đoàn Thanh niên | Bí thư Chi đoàn: chưa xác định số lượng; Phó Bí thư: 2 |
| Hội Nông dân | Chi hội trưởng: chưa xác định số lượng; Chi hội phó: 3 |
| Hội Phụ nữ | Chi hội trưởng: chưa xác định số lượng; Phó Chi hội trưởng: 3 |
| Hội Cựu chiến binh | Chi hội trưởng: chưa xác định số lượng; Chi hội phó: 3 |
| Chi hội Người cao tuổi | Giữ “Phó Chi hội Người cao tuổi”, chưa xác định định mức; chưa tự tạo chức danh đứng đầu |
| Tổ an ninh cơ sở | Giữ “Phó Tổ trưởng/Tổ phó”, chưa xác định định mức; chưa tự tạo chức danh Tổ trưởng |
| Đoàn thể xã hội về dân vũ | Chỉ danh mục tổ chức; chưa tạo chức danh hoặc thành viên |

Tổng cộng **10 tổ chức, 16 chức danh**: 9 chức danh có định mức rõ, 7 chưa có định mức.
Seed tạo **0 cư dân, 0 phân công, 0 hội viên**.

## Triển khai SQL an toàn

Project dùng database-first; không có EF Migrations. Chỉ sử dụng script SQL cho schema này.
Mapping EF được bổ sung qua `Models/MyStockContext.Organizations.cs`; không sửa `MyStockContext.cs`, không gọi Migrate/EnsureCreated từ ứng dụng.

File triển khai: `database/20261113_PortalOrganizations.sql`.
File rollback: `database/20261113_PortalOrganizations_Rollback.sql`.

**Chưa tự áp dụng lên database dùng chung `QuanLyCuDan`.** Script chỉ được kiểm thử trên database SQL Server độc lập có tiền tố `QuanLyCuDan_PortalTest_`.
Ứng dụng không tự chạy script. Trước triển khai cần được người phụ trách xác nhận target, bảo trì nếu cần và tạo bản backup mới.

Trong SSMS, bật **Query → SQLCMD Mode**. Tạo backup bằng tên file mới, không ghi đè bản backup có trước:

```sql
BACKUP DATABASE [TEN_DATABASE_DA_DUOC_DUYET]
TO DISK=N'DUONG_DAN_BACKUP_MOI.bak'
WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK=N'DUONG_DAN_BACKUP_MOI.bak' WITH CHECKSUM;
```

Đặt ba dòng SQLCMD sau ở đầu một bản chạy script (hoặc bỏ comment các dòng mẫu ở đầu file), rồi chạy toàn bộ script:

```sql
:setvar PortalTargetDatabase "TEN_DATABASE_DA_DUOC_DUYET"
:setvar PortalGroupCode "TO-A"
:setvar PortalBackupFile "DUONG_DAN_BACKUP_MOI.bak"
```

Script kiểm tra backup hoàn tất của đúng target trong msdb, chạy VERIFYONLY, kiểm tra COMPACT-2.0, lấy GROUP/AREA đang hoạt động,
dùng application lock + transaction + XACT_ABORT. Nếu có schema một phần/không nhận diện thì dừng để rà soát.
Unique code + INSERT WHERE NOT EXISTS ngăn seed trùng; không UPDATE/DELETE dữ liệu hiện hữu khi chạy lại.
Các SELECT cuối file kiểm tra địa bàn, tổ chức, chức danh/định mức và số phân công/hội viên thực tế.
Nếu một code danh mục đã bị chỉnh thủ công, script giữ nguyên thay vì ghi đè; người triển khai phải đối chiếu các SELECT sau chạy.

Rollback chỉ dành cho danh mục vừa triển khai, chưa sử dụng hoặc chỉnh sửa. Cần backup đã verify và biến `PortalAllowCatalogRemoval="YES"`.
Script từ chối nếu có hội viên/phân công, dữ liệu địa bàn khác hoặc số bản ghi ngoài danh mục dự kiến; chỉ xóa 4 bảng mới và marker tính năng.
Không dùng rollback này khi đã nhập dữ liệu thực tế hoặc sửa danh mục. Khi đó phục hồi backup vào database độc lập để đối chiếu,
không tự restore đè lên database chung vì có thể mất những cập nhật phát sinh sau backup.

## Hiển thị và bảo vệ dữ liệu

- Công khai: tổ chức phải hoạt động, `IsPublic=1`, đúng địa bàn đang cấu hình; detail ngoài phạm vi/ẩn/ngừng hoạt động trả 404.
- Chức danh tách riêng định mức theo tài liệu và nhân sự đang được phép hiển thị. Không có tên thì “Chưa cập nhật thông tin”.
- Tên nhân sự chỉ được đọc khi phân công hoạt động, đã xác nhận, `PublishName=1`, nằm trong thời gian hợp lệ và cư dân có hộ hiện tại tại địa bàn đó.
- ViewModel công khai chỉ chứa tên được phép; không chứa ID cư dân, CCCD, ngày sinh, địa chỉ nhà, điện thoại riêng hay danh sách hội viên.
- Cư dân phải qua bộ kiểm tra địa bàn/SELF permission hiện có. Người chưa có hộ trong onboarding không được mở các mục tổ chức nội bộ.
- “Hội nhóm của tôi” lấy ResidentId của tài khoản đăng nhập; bỏ qua ID người/tổ do trình duyệt gửi. Chỉ lấy hội viên/phân công đã xác nhận, còn hiệu lực, đúng địa bàn.
- Không có dữ liệu hội viên thì hiển thị trống, không suy ra hội nhóm từ tuổi, giới tính, vai trò tài khoản hoặc nghề nghiệp.
- Khi database chưa cài marker `PORTAL-ORG-1.0`, phần mới hiển thị “Thông tin tổ chức đang được cập nhật”, các chức năng cũ tiếp tục chạy.
  Không lấy catalog tĩnh hoặc dữ liệu tổ khác để thay thế database chưa triển khai.
- Thêm mục giấy tờ bằng danh sách tệp trên chính các request đã được phân quyền; giữ nguyên endpoint tải tệp và kiểm tra quyền.
- Từ chối tư cách cư dân trên trang GET có giao diện 403 dễ hiểu; không biến thành 200 hoặc nới quyền truy cập.

## Giao diện

Stylesheet mới `community-portal.css`, mọi selector nằm dưới `.portal-body`; không sửa CSS/layout quản trị.
Đỏ `#B71C1C`, đỏ đậm `#8E1717`, vàng `#D4A62A`, nền `#F5F6F8`, chữ `#263238`, viền `#E3E6EA`.
Header/menu/footer, trang chủ, card thông báo, directory/detail tổ chức, sidebar/tổng quan và các form cư dân dùng cùng hệ màu.
Icon SVG hình học được viết trong Razor, logo chữ CĐ trung tính. Không sử dụng VNeID, Quốc huy hay nhận diện của cơ quan nhà nước.
Menu mobile thu gọn trong luồng trang, sidebar cư dân cuộn trong vùng riêng. Bảng chức danh chuyển thành các khối trên điện thoại.
Form đăng ký/bổ sung dùng `_ResidentAccountLayout`; `/Account/Login?returnUrl=/Resident` dùng layout đó theo mục đích đăng nhập.
Đường dẫn login mặc định và trang quản trị vẫn dùng `_AccountLayout`/`admin.css`; không sửa controller/logic xác thực.
Trang đổi mật khẩu dùng chức năng tài khoản chung hiện có; không thiết kế lại layout quản trị được nó sử dụng.

## Chạy kiểm thử độc lập

```powershell
# Tạo database test mới bằng baseline demo có sẵn, backup database test rồi chạy 18 kiểm tra migration/rollback.
powershell -NoProfile -ExecutionPolicy Bypass -File .validation/organization-database-test.ps1 -Server localhost

dotnet build QuanLyCuDan/QuanLyCuDan.csproj --no-restore -o .local/organization-build
dotnet test .validation/PortalScope.Tests/PortalScope.Tests.csproj --artifacts-path .local/organization-test-artifacts
node --test .validation/auth-client.test.cjs
```

Runner database đổi toàn bộ CREATE/USE/DB_NAME guard của baseline sang tên test sinh riêng, kiểm tra lại trước khi thực thi.
Không chạy nguyên `QuanLyCuDan.sql` và không sửa file đó. Database test được giữ lại để review, không tự DROP.
Thông tin lần chạy thành công: `.local/organization-db-test/latest.json`; có tên database, backup và thư mục log.
Nếu có lần chạy trước dừng giữa chừng, artifacts/database test được giữ lại, không xóa dữ liệu tự động.

Để chạy ứng dụng trên database test (từ thư mục repository):

```powershell
$testInfo = Get-Content .local/organization-db-test/latest.json -Raw | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__MyStock = 'Server=localhost;Database=' + $testInfo.Database + ';Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
Set-Location QuanLyCuDan
dotnet ../.local/organization-build/QuanLyCuDan.dll --urls http://127.0.0.1:5108
```

Không đổi connection string local đang dùng để trỏ database nhóm sang database test; override chỉ áp dụng cho process QA trên.
Địa bàn TO-A vẫn lấy từ `appsettings.Development.json` hiện có.

Kiểm tra HTTP và Chrome (ở PowerShell khác, đặt mật khẩu demo qua môi trường, không ghi vào script):

```powershell
$env:AUTH_TEST_URL = 'http://127.0.0.1:5108'
$env:PORTAL_TEST_USER = 'dan01'
$env:PORTAL_TEST_OUTSIDE_USER = 'dan03'
$env:PORTAL_TEST_PASSWORD = '<mật khẩu demo hiện có>'
node --test .validation/organizations-http-smoke.cjs
node .validation/organizations-browser.cjs
```

## Hướng dẫn demo

1. Mở `/`: tên Tổ A, thông báo công khai, lối tắt, khu vực tổ chức.
2. Mở `/gioi-thieu`, `/co-cau-to-chuc`, lọc lĩnh vực; kiểm tra 10 tổ chức và tên “Ban lãnh đạo thôn” kèm ghi chú nguồn.
3. Mở chi tiết Chi bộ: Bí thư 1 người, Phó Bí thư 2–3, nhân sự chưa cập nhật. Xem Người cao tuổi/an ninh/dân vũ để thấy phần chưa rõ không bị tự suy diễn.
4. Mở `/hoi-nhom`: 5 đoàn thể/hội và nhóm dân vũ. Danh mục không có tên người mẫu.
5. Mở đăng ký: layout mới, TO-A cố định, validation và chờ duyệt giữ nguyên. Không cần tạo tài khoản mới để trình bày quy trình.
6. Đăng nhập dan01: các chức năng cũ, giấy tờ, hội nhóm và “Hội nhóm của tôi” trống vì chưa có dữ liệu tham gia được xác nhận.
7. Đăng nhập dan02 để so sánh quyền chủ hộ về khoản thu; dan03 để thấy cổng nội bộ TO-A bị từ chối với status 403.
8. Không gửi/hủy hồ sơ demo khi chỉ xem giao diện: YC-DEMO-001 vẫn PENDING, YC-DEMO-002 vẫn PROCESSING.

## Các điểm cần chủ đề tài xác nhận

- Cách gọi Trưởng thôn/Phó Trưởng thôn khi địa bàn là tổ dân phố.
- Chức danh đứng đầu Người cao tuổi, an ninh; tên đầy đủ của cấp phó và các định mức chưa có.
- Nhân sự thực tế, quyền công bố tên, thông tin hoạt động/mô tả tổ chức và dữ liệu hội viên đã xác nhận.
- Quy tắc phân công, nhiều nhiệm kỳ, bảo lưu hội viên khi chuyển địa bàn, quản lý và phê duyệt: chưa thuộc phạm vi hai cổng nên chưa phát triển.
- Xác nhận triển khai 4 bảng mới và catalog vào database dùng chung. Trước bước đó, bản đầy đủ chạy ở database test độc lập.

## Kết quả kiểm thử thực tế ngày 2026-11-13

| Kiểm tra | Kết quả cuối |
|---|---|
| Build project chính vào `.local/organization-final-build` | Thành công, 0 warning, 0 error |
| MVC/HTTP qua ứng dụng thật trên SQLite độc lập | **114 pass, 0 fail, 0 skip**: 87 hồi quy + 27 ca tổ chức/riêng tư mới |
| SQL Server: schema, seed hai lần, FK/unique/check, backup, rollback, snapshot | **18 pass, 0 fail** |
| HTTP mới trên SQL Server test | **9 pass, 0 fail, 0 skip** (test cha + 8 ca con) |
| Smoke public/resident cũ trên SQL Server test | **7 pass, 0 fail, 0 skip** (test cha + 6 ca con) |
| JavaScript xác thực hiện có | **6 pass, 0 fail, 0 skip** |
| Chrome headless | **122 kiểm tra đạt**, không lỗi JS hoặc tràn ngang; 390/768/1440 px |
| Kiểm tra source được bảo vệ | SHA256 của 13 file nghiệp vụ/admin/schema/SignalRChat giữ nguyên so với đầu nhiệm vụ |
| Whitespace | `git diff --check -- QuanLyCuDan database .validation` đạt |

Đã xem ảnh trang chủ desktop, chi tiết tổ chức mobile, đăng ký mobile và tổng quan cư dân desktop; chỉnh bảng chức danh mobile thành các khối để không cần kéo ngang.
Chức năng cũ được kiểm tra: công khai/tìm kiếm/detail/phân trang, đăng ký/chờ duyệt/validation, login/logout,
liên hệ và rowversion, thành viên hộ, yêu cầu/hủy/idempotency, phản ánh/trao đổi, upload/download quyền sở hữu,
khoản thu chủ hộ, thông báo nội bộ/cá nhân, CSRF và cư dân khác địa bàn.
Phần mới được kiểm tra: code/ID tổ chức, hoạt động/công khai, 404 ngoài phạm vi, quyền nội bộ, nguồn/định mức chưa rõ,
chấp thuận công bố tên, người khác địa bàn/nhân sự chưa xác nhận, nhiều hội, thông tin hội nhóm của chính mình và không có endpoint tự tham gia.
Hai người đã được xác nhận có cùng họ tên vẫn được giữ thành hai bản ghi hiển thị, không gộp người theo tên.

Database test chạy thành công: `QuanLyCuDan_PortalTest_20261113_141930_7e78b8a6` trên `localhost`.
Backup đã VERIFYONLY: `C:/Program Files/Microsoft SQL Server/MSSQL16.MSSQLSERVER/MSSQL/Backup/QuanLyCuDan_PortalTest_20261113_141930_7e78b8a6_before_organizations.bak`.
Lần thử trước dừng do trích dẫn đường dẫn backup có dấu cách; database test và backup lần đó được giữ lại, không tự xóa.
Runner đã sửa cách truyền biến SQLCMD; lần chạy đầy đủ sau đạt 18/18.

Đọc lại database dùng chung sau kiểm thử: vẫn **23 bảng, 0 bảng PortalOrganization**, **7 cư dân**;
`YC-DEMO-001=PENDING`, `YC-DEMO-002=PROCESSING`. Không áp dụng migration/rollback vào database này.
Các lần đăng nhập demo để kiểm tra riêng bản test chỉ ghi session/token/hash theo luồng xác thực trong database test.
Không ghi yêu cầu, cập nhật hộ, khoản thu, nhân sự hoặc hội viên lên database nhóm.

Artifacts ignored:

- `QuanLyCuDan/.local/test-results/organizations.trx`, `.local/organization-tests.log`.
- `.local/organization-db-test/latest.json` và thư mục log SQL được nó trỏ tới.
- `QuanLyCuDan/.local/organization-ui/latest.json` và thư mục ảnh được nó trỏ tới.
- `.local/organization-upgrade-before.patch`, `organization-upgrade-status-before.txt`, `organization-upgrade-status-after.txt`, `organization-upgrade-diff-stat.txt`, `organization-upgrade-protected-hashes.json`.

Không coi SQLite là bằng chứng đầy đủ cho constraint SQL Server; phần đó đã chạy riêng trên SQL Server thật.
Chưa kiểm chứng quy trình quản trị nhập/duyệt nhân sự vì không có đặc tả hoặc endpoint quản trị mới trong phạm vi.
Nhân sự và hội viên trên database test sau seed đang trống đúng yêu cầu; chỉ fixture trong test riêng mới có dữ liệu để kiểm tra quyền.

## File thay đổi trong riêng nhiệm vụ nâng cấp này

10 file hiện có được chỉnh tiếp, bảo toàn những sửa đổi từ nhiệm vụ giới hạn địa bàn:

| File | Thay đổi lần này |
|---|---|
| `Controllers/HomeController.cs` | Đọc preview danh mục cho trang chủ |
| `Controllers/ResidentController.cs` | Thêm ba section đọc documents/groups/my-groups và detail tổ chức; giữ gate, bổ sung trang GET 403 |
| `Models/ResidentPortalViewModel.cs` | Dữ liệu danh mục/hội nhóm của tôi cho view |
| `Views/Home/Index.cshtml` | Trang chủ đỏ–vàng, giới thiệu địa bàn, lối tắt và preview tổ chức |
| `Views/Resident/Index.cshtml` | Sidebar/icon, overview, giấy tờ và hội nhóm; giữ form/nghiệp vụ cũ |
| `Views/Shared/_PortalLayout.cshtml` | Header/menu/footer riêng, CSS mới, menu mobile |
| `Views/Account/Login.cshtml` | Chọn theme cư dân chỉ khi returnUrl thuộc `/Resident`; login mặc định không đổi theme |
| `Views/Account/Register.cshtml` | Layout đăng ký cư dân riêng |
| `Views/Account/ResidentRegistration.cshtml` | Layout bổ sung cư dân riêng |
| `wwwroot/js/portal.js` | Bổ sung toggle menu; giữ điều kiện form và chống submit lặp hiện có |

20 file mới:

- `QuanLyCuDan/Controllers/CommunityController.cs`.
- `QuanLyCuDan/Models/PortalOrganization.cs`, `MyStockContext.Organizations.cs`, `OrganizationPortalViewModel.cs`.
- `QuanLyCuDan/Portal/OrganizationReader.cs`, `ORGANIZATIONS.md`.
- `QuanLyCuDan/Views/Community/Index.cshtml`, `Detail.cshtml`, `About.cshtml`.
- `QuanLyCuDan/Views/Resident/AccessDenied.cshtml`.
- `QuanLyCuDan/Views/Shared/_PortalIcon.cshtml`, `_OrganizationCards.cshtml`, `_ResidentAccountLayout.cshtml`.
- `QuanLyCuDan/wwwroot/css/community-portal.css`.
- `database/20261113_PortalOrganizations.sql`, `20261113_PortalOrganizations_Rollback.sql`.
- `.validation/PortalScope.Tests/OrganizationHttpTests.cs`.
- `.validation/organization-database-test.ps1`, `organizations-browser.cjs`, `organizations-http-smoke.cjs`.

Các đường dẫn rút gọn trong bảng 10 file đều tính từ `QuanLyCuDan/`.
Không thay `AccountController` trong nhiệm vụ nâng cấp này; phần đăng ký đã sửa ở nhiệm vụ trước được giữ nguyên.

## Git cuối lượt rà soát

Branch: `feature/phan-cua-Luan`. Không commit/push/merge/reset.
`git diff --stat` so với HEAD gồm cả thay đổi có sẵn từ trước, **chưa tính file untracked**:

```text
 .validation/portal-browser.cjs                     |   5 +-
 .validation/registration-onboarding-smoke.cjs      |   8 +-
 .validation/resident-portal-smoke.cjs              |   8 +-
 QuanLyCuDan/Controllers/AccountController.cs       |  47 +++++---
 QuanLyCuDan/Controllers/HomeController.cs          |  30 +++--
 QuanLyCuDan/Controllers/ResidentController.cs      | 126 +++++++++++++++++----
 QuanLyCuDan/Models/ResidentPortalViewModel.cs      |  10 +-
 QuanLyCuDan/Views/Account/Login.cshtml              |   3 +-
 QuanLyCuDan/Views/Account/Register.cshtml           |   7 +-
 QuanLyCuDan/Views/Account/ResidentRegistration.cshtml | 4 +-
 QuanLyCuDan/Views/Home/Index.cshtml                |  23 ++--
 QuanLyCuDan/Views/Resident/Announcement.cshtml      |   2 +-
 QuanLyCuDan/Views/Resident/Index.cshtml             |  45 ++++++--
 QuanLyCuDan/Views/Shared/_PortalLayout.cshtml       |  27 +++--
 QuanLyCuDan/Views/Shared/_RegistrationProfileFields.cshtml | 4 +-
 QuanLyCuDan/wwwroot/js/portal.js                   |   6 +
 SignalRChat/Program.cs                            |   4 +-
 SignalRChat/appsettings.json                      |  14 +--
 18 files changed, 262 insertions(+), 111 deletions(-)
```

`git status --short`: 18 tracked files có trạng thái M như danh sách trên; các file mới và file untracked đã có từ nhiệm vụ trước vẫn chưa stage.
Danh sách đầy đủ, không rút gọn nằm trong `.local/organization-upgrade-status-after.txt`; `git ls-files --others --exclude-standard` liệt kê từng file untracked.
Hai file `SignalRChat` có thay đổi từ trước đã được đối chiếu SHA256, không phải thay đổi của nhiệm vụ này.
Không thay `AnnouncementsController`, `ManagementController`, `RegistrationsController`, `AdminController`, `BaseController`,
`AuthControllerBase`, `MyStockContext.cs`, `Program.cs`, `admin.css`, `_ManagementLayout.cshtml` hoặc `QuanLyCuDan.sql`.
