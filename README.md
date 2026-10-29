# MiniMES

Đồ án: **Xây dựng hệ thống quản lý lưu trình sản xuất (MES) tích hợp AI dự báo và gợi ý xử lý tiến độ**.

Tác giả: Vi Đức Đoàn · GVHD: TS. Vũ Đức Quang.

## Phạm vi đã bắt đầu triển khai

Mốc 1 gồm đăng nhập/đăng xuất, quyền FUN hai lớp, quản lý sản phẩm (thêm/sửa/ngừng sử dụng), validation, chống ghi đè phiên bản dữ liệu, transaction/audit và trang xem nhật ký. Web dùng **ASP.NET Core MVC + Razor + JavaScript thuần**, không cần Node.js, Vue hay frontend build riêng.

**Chưa phải hệ thống MES hoàn chỉnh.** Tài khoản mẫu được tạo qua CLI; chưa có màn hình quản trị tài khoản/quyền. Trạm/lỗi/routing, Work Order, barcode, Dashboard sản xuất, SignalR và AI là các mốc tiếp theo, chưa được triển khai. Xem [kế hoạch](docs/KE_HOACH_MINIMES.md) và [đặc tả/kết quả mốc 1](docs/GIAI_DOAN_01.md).

## Cấu trúc dễ theo dõi

```text
src/MiniMES.Web/
  Controllers/Pages/   Trang MVC trả Razor View
  Controllers/Api/     API kiểm tra quyền/DTO, gọi Service
  Dtos/                Đầu vào có Data Annotations
  Models/              Dữ liệu sản phẩm, tài khoản, nhật ký
  Services/            Nghiệp vụ, transaction, dữ liệu cũ/mới
  Repositories/        Dapper gọi Stored Procedure có tham số
  Security/            Cookie authentication và FUN filter
  Views/               Giao diện .cshtml
  wwwroot/             CSS và JavaScript thuần
database/              Schema, Stored Procedure, danh mục FUN
tests/MiniMES.Tests/   Kiểm thử HTTP và tích hợp SQL Server
docs/                  Thiết kế, tiến độ và kết quả kiểm thử
```

## 1. Chuẩn bị

- .NET SDK theo `global.json` (10.0.403, chấp nhận patch SDK cùng feature band).
- SQL Server 2022 Developer hoặc SQL Server tương thích, truy cập được từ máy chạy ứng dụng. Developer chỉ dùng cho phát triển/thử nghiệm.
- Git; Visual Studio hỗ trợ .NET 10 hoặc VS Code tùy lựa chọn. Không bắt buộc Docker trên máy cá nhân.

Trong cloud Linux x64, SDK có thể cài bằng `bash scripts/install-dotnet.sh`; sau đó dùng `bash scripts/dotnet.sh` thay cho `dotnet` nếu SDK chưa có trên PATH. Script giữ TLS verification và kiểm tra SHA512 chính thức. SQL Server vẫn phải được chuẩn bị riêng.

Tạo database rỗng bằng SSMS hoặc công cụ SQL Server bạn đang dùng:

```sql
IF DB_ID(N'MiniMES') IS NULL CREATE DATABASE [MiniMES];
```

Cấu hình connection string bằng biến `ConnectionStrings__MiniMes` hoặc User Secrets của project. **Không ghi mật khẩu vào source, script SQL hay file được Git theo dõi.** Có thể dùng `appsettings.Local.json` đã được ignore nếu lưu cục bộ và bảo vệ quyền đọc file.

Connection string cần trỏ tới database `MiniMES`, không phải `master`. Sử dụng mã hóa và chứng chỉ SQL Server hợp lệ với `Encrypt=True;TrustServerCertificate=False`. Với Windows có thể dùng Windows Authentication nếu tài khoản tiến trình có quyền phù hợp; không mặc định cần tài khoản `sa`. Cấu hình chứng chỉ theo môi trường, không tắt kiểm tra TLS để bỏ qua lỗi kết nối. Cấp quyền tạo schema/SP cho tài khoản chạy khởi tạo; tài khoản ứng dụng khi triển khai chỉ cần quyền thực thi các SP cần thiết.

Ví dụ PowerShell nhập connection string mà không hiển thị giá trị:

```powershell
$env:ConnectionStrings__MiniMes = [System.Net.NetworkCredential]::new('', (Read-Host 'Connection string SQL Server' -AsSecureString)).Password
```

Linux/Bash:

```bash
read -rsp 'Connection string SQL Server: ' ConnectionStrings__MiniMes
export ConnectionStrings__MiniMes
```

## 2. Restore, build và khởi tạo

Chạy tại thư mục repository:

```bash
dotnet restore MiniMES.slnx --locked-mode
dotnet build MiniMES.slnx --no-restore
dotnet run --project src/MiniMES.Web --no-build -- --initialize-database
```

Lệnh khởi tạo chạy các file `database/*.sql` cố định theo tên, không ghép SQL từ request. Chạy lại không xóa dữ liệu hoặc tự đổi mật khẩu. Đây là bộ script mốc đầu, chưa thay thế hệ thống migration đầy đủ cho các thay đổi schema về sau. Không sửa bảng đang dùng bằng cách tùy tiện sửa lại script cũ; bổ sung migration có phiên bản ở mốc tiếp theo.

Tạo hai tài khoản phát triển `admin` và `viewer` bằng cách nhập mật khẩu riêng (12–128 ký tự):

```powershell
$env:MINIMES_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Mật khẩu admin cho DB phát triển' -AsSecureString)).Password
$env:MINIMES_VIEWER_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Mật khẩu viewer cho DB phát triển' -AsSecureString)).Password
dotnet run --project src/MiniMES.Web --no-build -- --seed-demo-users
Remove-Item Env:MINIMES_ADMIN_PASSWORD, Env:MINIMES_VIEWER_PASSWORD
```

Linux có thể dùng `read -rsp` rồi `export` cho hai biến tương ứng, chạy cùng lệnh CLI và `unset` sau đó. Không có mật khẩu mặc định trong dự án. Mật khẩu được băm bằng `PasswordHasher`; bootstrap không sửa tài khoản đã tồn tại. `admin` nhận năm FUN ban đầu; `viewer` chỉ có `MODEL_VIEW`. Việc tạo tài khoản/quyền mẫu và audit diễn ra cùng transaction cho từng tài khoản, audit không chứa hash/mật khẩu.

## 3. Chạy web

```bash
dotnet run --project src/MiniMES.Web --no-build -- --environment Development --urls http://localhost:5080
```

Trên máy phát triển, mở cổng 5080 bằng trình duyệt, đăng nhập bằng tài khoản đã bootstrap. Cloud onboarding không có preview localhost; dùng các request nội bộ để kiểm tra.

- `GET /health/live`: tiến trình web phản hồi.
- `GET /health/ready`: trả 200 khi kiểm tra DB/FUN thành công; 503 khi DB chưa sẵn sàng. Endpoint này không thay thế kiểm thử CRUD/transaction.

Đăng nhập → Sản phẩm → thêm/sửa/ngừng sử dụng → Nhật ký thay đổi. `viewer` không thấy form/nút thay đổi và bị API từ chối nếu tự gửi request.

Production cần HTTPS, host hợp lệ trong `AllowedHosts`, tài khoản DB giới hạn quyền và cấu hình lưu Data Protection keys an toàn. Khi triển khai nhiều instance hoặc reverse proxy phải cấu hình thêm khóa dùng chung/proxy tin cậy; chưa được xác minh ở mốc 1. Không chạy chế độ Development ra Internet để tránh cấu hình bảo mật yếu hơn.

## 4. Gọi API và CSRF

Quyền FUN không thay thế bảo vệ CSRF. Quy trình Postman/công cụ tương đương:

1. `GET /api/auth/csrf`, giữ cookie và lấy trường `token`.
2. `POST /api/auth/login` với JSON `username`, `password` và header `X-CSRF-TOKEN` bằng token bước 1. Dùng tài khoản/mật khẩu đã bootstrap, không có thông tin cố định trong ví dụ.
3. Lấy lại token bằng `GET /api/auth/csrf` sau khi đăng nhập, vì danh tính đã đổi.
4. Gửi API thay đổi dữ liệu với cookie đăng nhập và token mới trong header.

Ví dụ nội dung `POST /api/products`:

```json
{"modelCode":"SP001","modelName":"Sản phẩm mẫu","description":"Mẫu dùng thử"}
```

Kết quả mong đợi: 201 và đối tượng có `id`, `version: 1`. `PUT /api/products/{id}` gửi các trường sản phẩm và `version` nhận từ lần đọc. `DELETE` gửi JSON `{"version":1}` (dùng phiên bản hiện tại). Thiếu quyền trả 403; DTO sai/thiếu CSRF trả 400; mã trùng hoặc phiên bản cũ trả 409. Xóa là ngừng sử dụng, vẫn giữ bản ghi và audit.

Danh sách endpoint/FUN đầy đủ ở [đặc tả mốc 1](docs/GIAI_DOAN_01.md).

## 5. Kiểm thử

```bash
dotnet test MiniMES.slnx --no-build --no-restore --logger "trx;LogFileName=phase-01.trx"
```

Nhóm HTTP security/validation không cần SQL Server, dùng danh tính giả **chỉ trong test assembly**. Nó kiểm tra filter, CSRF, DTO và render Razor, không chứng minh đăng nhập cookie hoặc CRUD với DB hoạt động.

Nhóm SQL integration cần một DB thử riêng. Tạo trước bằng SQL Server:

```sql
IF DB_ID(N'MiniMES_Tests') IS NULL CREATE DATABASE [MiniMES_Tests];
```

Thiết lập bí mật ngoài Git:

- `MINIMES_TEST_CONNECTION_STRING`: connection string tới đúng `MiniMES_Tests`.
- `MINIMES_TEST_ADMIN_PASSWORD`, `MINIMES_TEST_VIEWER_PASSWORD`: mật khẩu bootstrap riêng, nhập an toàn như phần phát triển.

Sau đó chạy:

```bash
dotnet test tests/MiniMES.Tests --no-build --no-restore --filter Category=SqlServer
```

Fixture từ chối DB có tên khác để tránh chạy fault injection trên dữ liệu phát triển/production. Ca rollback tạo và gỡ trigger cố ý gây lỗi audit trong DB test. Nhóm này tạo dữ liệu có mã ngẫu nhiên và giữ lại để kiểm tra; mỗi lần chạy thêm dữ liệu. Chạy lại dùng cùng mật khẩu test; bootstrap không đặt lại mật khẩu tài khoản đã có. Chỉ xóa/tạo lại DB thử khi chủ động muốn dọn dữ liệu.

**Thiếu connection string → 8 ca SQL được ghi SKIPPED, không phải PASSED.** Phải cung cấp SQL Server và chạy lại trước khi xác nhận đăng nhập thật, CRUD, rollback và concurrency đã đạt.
