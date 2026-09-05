# HỆ THỐNG CẤP PHÁT VÀ KIỂM TRA VĂN BẰNG CHỨNG CHỈ SỐ

Dự án phát triển bằng **C# .NET 8 (ASP.NET Core Web API)** kết hợp cơ sở dữ liệu **Microsoft SQL Server**, sử dụng thuật toán mã hóa **RSA 2048-bit** để ký số và thuật toán băm **SHA-256** nhằm kiểm tra tính toàn vẹn và chống làm giả văn bằng số.

---

## 1. Cấu Trúc Thư Mục Dự Án

```text
Cấp phát văn băng chứng chi sổ/
│
├── Database/
│   └── HeThongVanBangSo.sql          # Kịch bản tạo Database, 5 bảng, Index, Stored Procedure & Dữ liệu mẫu
│
├── Models/                           # Các Entity Class ánh xạ 5 bảng trong CSDL
│   ├── DonViPhatHanh.cs              # Bảng Đơn vị cấp phát (Trường, Sở, Bộ...)
│   ├── NguoiNhan.cs                  # Bảng Người nhận văn bằng (Họ tên, CCCD, Ngày sinh)
│   ├── KhoaKySo.cs                   # Bảng Lưu khóa RSA (Public Key, Private Key mã hóa)
│   ├── VanBangChungChi.cs            # Bảng Văn bằng (Đường dẫn file, Chữ ký số, Mã băm SHA-256)
│   └── LichSuKiemTra.cs              # Bảng Lưu nhật ký các lần người dùng tải file lên đối chiếu
│
├── Data/
│   └── HeThongVanBangDbContext.cs    # EF Core DbContext cấu hình quan hệ, chỉ mục & khóa ngoại
│
├── DTOs/                             # Các lớp dữ liệu trao đổi (Request/Response)
│   ├── TaoDonViDto.cs
│   ├── TaoNguoiNhanDto.cs
│   ├── SinhKhoaDto.cs
│   ├── CapVanBangDto.cs
│   ├── KiemTraVanBangDto.cs
│   └── KetQuaKiemTraDto.cs
│
├── Services/                         # Dịch vụ mã hóa & chữ ký số
│   ├── ISignatureService.cs
│   └── SignatureService.cs           # Xử lý sinh cặp khóa RSA, băm SHA-256 tệp và ký/xác thực chữ ký
│
├── Controllers/                      # Các API Controllers nghiệp vụ
│   ├── DonViPhatHanhController.cs    # CRUD Đơn vị phát hành
│   ├── NguoiNhanController.cs        # Quản lý hồ sơ người nhận
│   ├── KhoaKySoController.cs         # Quản lý và sinh cặp khóa RSA
│   ├── VanBangChungChiController.cs  # Nghiệp vụ cấp bằng, băm file, ký số và thu hồi
│   └── KiemTraVanBangController.cs   # Nghiệp vụ tải file kiểm tra, đối chiếu mã băm và ghi log
│
├── appsettings.json                  # Cấu hình chuỗi kết nối SQL Server
├── Program.cs                        # Cấu hình Web API, Dependency Injection và Swagger UI
└── HeThongVanBangSo.csproj           # File project .NET 8
```

---

## 2. Hướng Dẫn Cài Đặt Cơ Sở Dữ Liệu SQL Server

1. Mở công cụ **SQL Server Management Studio (SSMS)** hoặc **Azure Data Studio**.
2. Kết nối tới SQL Server của bạn (ví dụ: `localhost`, `.\SQLEXPRESS` hoặc `(localdb)\mssqllocaldb`).
3. Mở tệp tin: `Database/HeThongVanBangSo.sql`.
4. Nhấn **Execute (F5)** để chạy toàn bộ kịch bản. Kịch bản sẽ tự động:
   - Tạo CSDL `HeThongVanBangSo`.
   - Tạo đủ 5 bảng: `DonViPhatHanh`, `NguoiNhan`, `KhoaKySo`, `VanBangChungChi`, `LichSuKiemTra`.
   - Tạo các chỉ mục `IDX_VanBang_MaBam`, `IDX_VanBang_SoHieu`, `IDX_NguoiNhan_SoCCCD`.
   - Tạo Stored Procedure `sp_KiemTraVanBangByHash`.
   - Nạp dữ liệu mẫu (Seed data) để sẵn sàng kiểm thử.

---

## 3. Cấu Hình Chuỗi Kết Nối Trong `appsettings.json`

Mở file `appsettings.json` và kiểm tra chuỗi kết nối phù hợp với máy tính của bạn:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=HeThongVanBangSo;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```
*Lưu ý:* Nếu dùng SQL Server Express, bạn đổi `Server=localhost` thành `Server=.\\SQLEXPRESS`.

---

## 4. Cách Khởi Chạy Ứng Dụng

### Cách 1: Chạy bằng Visual Studio
1. Mở Visual Studio.
2. Chọn **Open a project or solution** và trỏ đến file `HeThongVanBangSo.csproj`.
3. Nhấn phím **F5** hoặc nút **Start Debugging**.
4. Trình duyệt sẽ tự động mở giao diện tài liệu trực quan **Swagger UI** tại `http://localhost:5000` (hoặc cổng tương ứng).

### Cách 2: Chạy bằng dòng lệnh (Terminal)
```bash
cd "Cấp phát văn băng chứng chi sổ"
dotnet run
```
Sau đó mở trình duyệt truy cập: `http://localhost:5000`

---

## 5. Quy Trình Nghiệp Vụ Chính Trong Hệ Thống

### Bước 1: Sinh Cặp Khóa Ký Số Cho Đơn Vị Phát Hành
- Gửi yêu cầu `POST /api/KhoaKySo/sinh-khoa` với `MaDonVi`.
- Hệ thống tự động sinh cặp khóa RSA 2048-bit:
  - Khóa công khai (`PublicKeyText`) dùng để kiểm tra tính nguyên bản.
  - Khóa bí mật (`PrivateKeyMaHoa`) dùng để ký tệp tin văn bằng.

### Bước 2: Cấp Văn Bằng Dưới Dạng Tệp Tin Có Ký Số
- Gửi yêu cầu `POST /api/VanBangChungChi/cap-van-bang` dạng `multipart/form-data`:
  - `MaDonVi`, `MaNguoiNhan`, `TenVanBang`, `SoHieu`, `NgayCap`.
  - `FileVanBang`: Tải file PDF văn bằng gốc lên.
- **Hệ thống xử lý:**
  1. Đọc stream của tệp tin PDF.
  2. Băm nội dung bằng thuật toán **SHA-256** tạo ra chuỗi 64 ký tự hex (`MaBamSHA256`).
  3. Sử dụng Private Key của đơn vị ký lên nội dung tệp tin tạo ra chuỗi Base64 (`ChuKySo`).
  4. Lưu tệp tin vào thư mục máy chủ và lưu thông tin vào bảng `VanBangChungChi`.

### Bước 3: Giao Diện Người Dùng Kiểm Tra Văn Bằng Hợp Lệ
- Người dùng (nhà tuyển dụng, học viên, cơ quan...) tải bất kỳ tệp tin PDF nào lên endpoint:
  `POST /api/KiemTraVanBang/upload-va-kiem-tra`.
- **Hệ thống xử lý:**
  1. Băm nội dung file tải lên theo chuẩn **SHA-256**.
  2. Dò tìm trong bảng `VanBangChungChi` xem có mã băm nào trùng khớp không (`IDX_VanBang_MaBam`).
  3. Nếu khớp:
     - Dùng Public Key của đơn vị phát hành xác thực lại `ChuKySo`.
     - Kiểm tra trạng thái văn bằng (`HOP_LE` hay `THU_HOI`).
     - Trả về: **HỢP LỆ**, tên người nhận, số CCCD, đơn vị cấp, số hiệu, ngày cấp.
  4. Nếu không khớp hoặc file đã bị sửa đổi dù chỉ 1 ký tự:
     - Trả về: **KHÔNG HỢP LỆ / FILE BỊ LÀM GIẢ**.
  5. Tự động ghi lại kết quả vào bảng `LichSuKiemTra` (Tên file, Mã băm, Kết quả 1/0, IP người kiểm tra, Thời gian).
