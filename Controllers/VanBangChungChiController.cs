using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;
using HeThongVanBangSo.Services;

namespace HeThongVanBangSo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VanBangChungChiController : ControllerBase
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ISignatureService _signatureService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VanBangChungChiController> _logger;

        public VanBangChungChiController(
            HeThongVanBangDbContext context,
            ISignatureService signatureService,
            IWebHostEnvironment env,
            ILogger<VanBangChungChiController> logger)
        {
            _context = context;
            _signatureService = signatureService;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Nghiệp vụ cốt lõi: Cấp mới văn bằng chứng chỉ dưới dạng tệp tin có ký số
        /// </summary>
        [HttpPost("cap-van-bang")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<VanBangChungChi>> CapVanBang([FromForm] int maDonVi, [FromForm] long maNguoiNhan, [FromForm] int? maKhoa, [FromForm] string tenVanBang, [FromForm] string soHieu, [FromForm] DateTime ngayCap, [FromForm] IFormFile fileVanBang)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 1. Kiểm tra đơn vị phát hành
            var donVi = await _context.DonViPhatHanhs.FindAsync(maDonVi);
            if (donVi == null || !donVi.TrangThaiHoatDong)
            {
                return BadRequest(new { message = "Đơn vị phát hành không tồn tại hoặc đã ngừng hoạt động." });
            }

            // 2. Kiểm tra người nhận
            var nguoiNhan = await _context.NguoiNhans.FindAsync(maNguoiNhan);
            if (nguoiNhan == null)
            {
                return BadRequest(new { message = "Người nhận văn bằng không tồn tại." });
            }

            // 3. Kiểm tra số hiệu văn bằng trùng lặp
            bool isSoHieuExist = await _context.VanBangChungChis.AnyAsync(v => v.SoHieu == soHieu.Trim());
            if (isSoHieuExist)
            {
                return Conflict(new { message = $"Số hiệu văn bằng '{soHieu}' đã tồn tại." });
            }

            // 4. Lấy khóa ký số còn hiệu lực của đơn vị
            KhoaKySo? khoaKy = null;
            if (maKhoa.HasValue)
            {
                khoaKy = await _context.KhoaKySos.FirstOrDefaultAsync(k =>
                    k.MaKhoa == maKhoa.Value &&
                    k.MaDonVi == maDonVi &&
                    k.TrangThai &&
                    k.NgayHetHan > DateTime.UtcNow);
            }
            else
            {
                khoaKy = await _context.KhoaKySos
                    .Where(k => k.MaDonVi == maDonVi && k.TrangThai && k.NgayHetHan > DateTime.UtcNow)
                    .OrderByDescending(k => k.MaKhoa)
                    .FirstOrDefaultAsync();
            }

            if (khoaKy == null)
            {
                return BadRequest(new { message = "Đơn vị phát hành chưa có khóa ký số hợp lệ hoặc khóa đã hết hạn. Hãy tạo khóa mới trước khi ký cấp bằng." });
            }

            // 5. Đọc tệp tin văn bằng (PDF)
            if (fileVanBang == null || fileVanBang.Length == 0)
            {
                return BadRequest(new { message = "Tệp tin tải lên rỗng." });
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await fileVanBang.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            // 6. TRỌNG TÂM: Tính mã băm SHA-256 và sinh Chữ ký số RSA
            string maBamSha256 = _signatureService.ComputeSha256Hash(fileBytes);
            string chuKySo = _signatureService.SignData(fileBytes, khoaKy.PrivateKeyMaHoa);

            // 7. Lưu file vào thư mục lưu trữ an toàn trên máy chủ
            string uploadsFolder = Path.Combine(_env.ContentRootPath, "Uploads", "Certificates");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string safeFileName = $"{soHieu.Trim()}_{Path.GetFileName(fileVanBang.FileName)}";
            string filePath = Path.Combine(uploadsFolder, safeFileName);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            string duongDanLuuTru = $"/Uploads/Certificates/{safeFileName}";

            // 8. Lưu vào bảng VanBangChungChi
            var vanBang = new VanBangChungChi
            {
                MaDonVi = maDonVi,
                MaNguoiNhan = maNguoiNhan,
                MaKhoa = khoaKy.MaKhoa,
                TenVanBang = tenVanBang.Trim(),
                SoHieu = soHieu.Trim(),
                DuongDanFileDaKy = duongDanLuuTru,
                ChuKySo = chuKySo,
                MaBamSHA256 = maBamSha256,
                NgayCap = ngayCap.Date,
                TrangThai = "HOP_LE",
                NgayTao = DateTime.UtcNow
            };

            _context.VanBangChungChis.Add(vanBang);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBySoHieu), new { soHieu = vanBang.SoHieu }, vanBang);
        }

        /// <summary>
        /// Lấy danh sách văn bằng đã cấp (Hỗ trợ phân trang và tìm kiếm)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VanBangChungChi>>> GetDanhSach([FromQuery] string? tuKhoa)
        {
            var query = _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();
                query = query.Where(v =>
                    v.SoHieu.Contains(tuKhoa) ||
                    v.TenVanBang.Contains(tuKhoa) ||
                    v.NguoiNhan!.HoTen.Contains(tuKhoa) ||
                    v.NguoiNhan.SoCCCD.Contains(tuKhoa));
            }

            var result = await query
                .OrderByDescending(v => v.NgayTao)
                .ToListAsync();

            return Ok(result);
        }

        /// <summary>
        /// Tra cứu thông tin văn bằng theo Số hiệu duy nhất
        /// </summary>
        [HttpGet("{soHieu}")]
        public async Task<ActionResult<VanBangChungChi>> GetBySoHieu(string soHieu)
        {
            var v = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .FirstOrDefaultAsync(v => v.SoHieu == soHieu.Trim());

            if (v == null)
            {
                return NotFound(new { message = $"Không tìm thấy văn bằng có số hiệu '{soHieu}'" });
            }

            return v;
        }

        /// <summary>
        /// Thu hồi hoặc hủy hiệu lực của một văn bằng
        /// </summary>
        [HttpPut("thu-hoi/{maVanBang:long}")]
        public async Task<IActionResult> ThuHoiVanBang(long maVanBang, [FromQuery] string? lyDo)
        {
            var vanBang = await _context.VanBangChungChis.FindAsync(maVanBang);
            if (vanBang == null)
            {
                return NotFound(new { message = $"Không tìm thấy văn bằng có mã {maVanBang}" });
            }

            vanBang.TrangThai = "THU_HOI";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Văn bằng số hiệu '{vanBang.SoHieu}' đã bị thu hồi.",
                lyDo = lyDo ?? "Không nêu rõ",
                thoiGian = DateTime.UtcNow
            });
        }
    }
}
