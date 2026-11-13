# Đồng bộ giao diện toàn website

Phạm vi lần này là trình bày của toàn bộ project QuanLyCuDan, bao gồm quản trị. Không sửa Controller, Model, SQL, phân quyền hoặc thuật toán xác thực. Không áp dụng thay đổi database từ nhiệm vụ trước.

## Kiểm kê thực tế

Đã rà soát 33 file Razor: 22 view nội dung, 5 layout, 4 partial và 2 file cấu hình view. Danh sách nguồn đầy đủ: `.local/ui-view-inventory.txt`; route attribute đã trích xuất trong `.local/ui-routes.txt`.

| View/nhóm route | Layout | Kết quả khảo sát và chỉnh sửa |
|---|---|---|
| Home: `/`, `/thong-bao/{id}`, `/Home/Privacy` | Portal | Đã đỏ từ trước; chuyển biến màu sang tokens dùng chung, giữ bố cục |
| Community: `/gioi-thieu`, `/co-cau-to-chuc`, `/hoi-nhom`, `/to-chuc/{id}` | Portal | Đã đỏ; dùng cùng tokens và nhận diện demo |
| Resident: Index (11 section), Detail, Announcement, AccessDenied | Portal | Giữ nghiệp vụ; thống nhất hover/focus/tokens |
| `/Account/Login` | Account | Xanh cũ; đổi sang đỏ, nền sáng và logo CĐ trung tính |
| Login có returnUrl Resident, Register, ResidentRegistration | ResidentAccount | Đồng bộ tokens; không đổi điều kiện layout hay form |
| `/Account/Profile`, modal đổi mật khẩu | Management | Xanh cũ; đồng bộ sidebar, input, nút và dialog |
| `/Admin?tab=users/roles/permissions` | Management | Xanh cũ; sidebar đỏ đậm, active vàng, bảng/card nền sáng |
| `/Registrations` và các trạng thái | Management | Dùng theme mới qua layout; giữ nguyên dialog và form duyệt |
| `/Management/{module}` | Management | 12 module hiện có dùng một view; đổi trình bày, không bổ sung chức năng còn placeholder |
| Announcements Index/Edit/Detail | Management | Đổi thương hiệu cũ; giữ màu trạng thái duyệt/đăng và toàn bộ xử lý |
| `/SignalR` | Default | Bootstrap xanh; thống nhất tokens/navbar/nút/focus, không đổi script kết nối |
| `/Home/Error` → Shared/Error | Default | Đổi navbar/nút xanh, thông báo tiếng Việt; giữ mã yêu cầu |

Không có view 404/500 riêng ngoài Shared/Error; các phản hồi Problem JSON của middleware/API không phải trang HTML. Giữ nguyên status và nội dung nghiệp vụ của chúng, không tự tạo cơ chế xử lý lỗi mới.

## Design system

`wwwroot/css/design-tokens.css` là nguồn màu chung:

| Token | Giá trị |
|---|---|
| primary / dark / hover | #B71C1C / #8E1717 / #9F1919 |
| accent | #D4A62A |
| background / surface | #F5F6F8 / #FFFFFF |
| text-primary / text-secondary | #263238 / #667085 |
| border | #E3E6EA |

Có tokens success/warning/error riêng. Xanh lá ở trạng thái thành công/đã duyệt và xanh ở trạng thái đang xử lý được giữ có chủ đích; không dùng làm thương hiệu. Không chỉnh file vendor Bootstrap/jQuery.

Cả 5 layout liên kết tokens. CSS entry point cũng import tokens để các consumer cũ không mất biến màu khi chưa restart bản build. `portal.css` cũ chỉ còn compatibility imports, không giữ một theme xanh song song.
`community-portal.css` dùng alias đến tokens chung. `admin.css` bỏ biến --green, thay màu nền/chữ xanh cũ theo ngữ nghĩa. CSS scoped của Default layout và site.css đổi màu Bootstrap và focus.
Focus trên nền sáng dùng đỏ; trong sidebar đỏ dùng vàng để dễ nhận biết. Modal giữ nguyên native dialog, ID và JavaScript; chỉ đổi màu, viền, kích thước và khoảng cách.
Không dùng Quốc huy, Quốc hiệu, logo VNeID hoặc biểu tượng Đảng; dùng logo chữ CĐ, icon trung tính và nhãn hệ thống demo học tập.

## File chỉnh sửa

- `Views/Shared/_PortalLayout.cshtml`, `_ResidentAccountLayout.cshtml`, `_AccountLayout.cshtml`, `_ManagementLayout.cshtml`, `_Layout.cshtml`.
- `Views/Shared/_Layout.cshtml.css`, `Views/Shared/Error.cshtml`.
- `wwwroot/css/admin.css`, `community-portal.css`, `portal.css`, `site.css`.
- Mới: `wwwroot/css/design-tokens.css`, tài liệu này, `.validation/unified-ui-browser.cjs`, `.validation/unified-signalr-browser.cjs`.

Các view nội dung không có màu inline/HEX riêng nên nhận theme qua layout; không sửa chúng hàng loạt. Các partial không có màu độc lập không cần sửa. Không sửa JavaScript sản phẩm, Controller, Model, SQL, config kết nối hoặc SignalRChat.

## Kiểm chứng

- Build project chính thành công: 0 warning, 0 error; output cuối `.local/unified-ui-release`.
- `dotnet test .validation/PortalScope.Tests/PortalScope.Tests.csproj --artifacts-path .local/organization-test-artifacts --no-restore`: 114 pass, 0 fail, 0 skip; log `.local/unified-ui-tests.log`, TRX `QuanLyCuDan/.local/test-results/unified-ui.trx`.
- Browser thực tế qua Chrome headless: công khai, tài khoản, 11 section cư dân, chi tiết yêu cầu/thông báo, 3 tab Admin, 3 trạng thái đăng ký, 11 module quản lý trực tiếp và các màn soạn/xem/sửa thông báo.
- Kích thước: 390 px, tablet 768 px, laptop 1366×768, desktop 1920×1080. Kiểm tra tràn ngang, tokens, sidebar, mở/đóng modal mật khẩu và thao tác quản trị; không submit thao tác thay đổi nghiệp vụ.
- SignalR được mở riêng bằng browser, kiểm tra tokens/bố cục và không ghi nhận exception JavaScript trong lần chạy.
- Kết quả browser mở rộng cuối: **301 kiểm tra đạt**, không lỗi JavaScript hoặc tràn ngang; SignalR thêm 2 kiểm tra đạt. Test JavaScript xác thực: **6 pass, 0 fail, 0 skip**.
- Đã xem ảnh đăng nhập mobile và quản trị laptop. Ảnh/result mới nhất: `QuanLyCuDan/.local/unified-ui-latest.json`.
- SHA256 toàn bộ Controllers, Models và script trong database khớp snapshot đầu nhiệm vụ: `.local/ui-business-before.json`.
- Không có SQL/schema migration hoặc cập nhật dữ liệu nghiệp vụ; đăng nhập kiểm thử tạo session/token bình thường trong database test đã có.

Giới hạn: form bổ sung hồ sơ được kiểm tra qua HTTP fixture trong bộ 114 test, chưa mở bằng tài khoản SQL demo đủ điều kiện trên browser. Không đổi mật khẩu thật, duyệt hồ sơ hoặc ghi thu tiền để thử giao diện; các nút modal chỉ mở/đóng/hủy. Các module quản lý còn placeholder được kiểm tra đúng trang đang có, không tuyên bố đã có CRUD nghiệp vụ.

Một lần chạy browser liên tiếp gặp timeout sau giới hạn refresh; giữ nguyên rate limit, chạy lại trên process QA mới. Không sửa bảo mật để vượt kiểm thử.
Selector tiếng Việt trong script test từng bị lỗi encoding khi truyền qua shell; đã sửa selector bằng Unicode code points và chạy lại đạt. Không thay JavaScript nghiệp vụ để xử lý lỗi của bộ test.

Tổng thay đổi riêng đợt này: 11 file trình bày hiện có và 4 file mới (tokens, 2 script browser, báo cáo). `git diff --stat` toàn workspace tại lúc kiểm tra: 26 tracked files, 337 dòng thêm/183 dòng bớt, gồm cả sửa đổi có sẵn từ các nhiệm vụ trước và chưa tính file untracked. Xem hai file Git snapshot để có kết quả đầy đủ; không quy những thay đổi Controller/Model/SQL có từ trước thành sửa đổi UI lần này.

## Xem và chạy

Bản UI kiểm thử chạy tại `http://127.0.0.1:5110/` trên database test có sẵn, không đổi kết nối local của bạn. Dùng tài khoản demo hiện có; mở `/Account/Login`, `/Account/Profile`, `/Admin` hoặc `/Announcements` theo đúng quyền.
Để dùng source mới ở :5044: dừng phiên debug hiện tại rồi build/run lại bằng Visual Studio hoặc `dotnet run --project QuanLyCuDan --launch-profile http`.

Kiểm thử browser cần AUTH_TEST_URL, PORTAL_TEST_USER và PORTAL_TEST_PASSWORD qua môi trường, không ghi mật khẩu vào script.
Git status và diff stat đầy đủ: `.local/ui-git-status.txt`, `.local/ui-diff-stat.txt`. Bao gồm cả thay đổi có từ trước; không phải mọi file dirty đều thuộc lần UI này. Không commit/push/merge/reset.
