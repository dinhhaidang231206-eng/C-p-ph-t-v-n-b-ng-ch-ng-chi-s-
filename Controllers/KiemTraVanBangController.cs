using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using System.Text.Json;
using HeThongVanBangSo.Models;
using HeThongVanBangSo.Services;

namespace HeThongVanBangSo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KiemTraVanBangController : ControllerBase
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ISignatureService _signatureService;
        private readonly ILogger<KiemTraVanBangController> _logger;

        public KiemTraVanBangController(
            HeThongVanBangDbContext context,
            ISignatureService signatureService,
            ILogger<KiemTraVanBangController> logger)
        {
            _context = context;
            _signatureService = signatureService;
            _logger = logger;
        }

        /// <summary>
        /// TRỌNG TÂM NGHIỆP VỤ: Tải tệp tin văn bằng lên để băm SHA-256 đối chiếu và kiểm tra tính hợp lệ
        /// </summary>
        [HttpPost("upload-va-kiem-tra")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> KiemTraBangFileTaiLen(IFormFile fileVanBang)
        {
            if (fileVanBang == null || fileVanBang.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng đính kèm tệp tin văn bằng (PDF) để kiểm tra." });
            }

            // 1. Đọc nội dung tệp tin tải lên
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await fileVanBang.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // 2. Tính toán mã băm SHA-256 từ tệp tin tải lên
            string maBamFile = _signatureService.ComputeSha256Hash(fileBytes);
            string tenFile = Path.GetFileName(fileVanBang.FileName);
            string? ipClient = HttpContext.Connection.RemoteIpAddress?.ToString();

            // 3. Đối chiếu với mã băm trong Cơ sở dữ liệu (Bảng VanBangChungChi)
            var vanBang = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .Include(v => v.KhoaKySo)
                .FirstOrDefaultAsync(v => v.MaBamSHA256 == maBamFile);

            bool ketQuaHopLe = false;
            bool chuKySoHopLe = false;
            string ghiChu = string.Empty;

            if (vanBang != null)
            {
                // Kiểm tra trạng thái hiệu lực
                if (vanBang.TrangThai == "THU_HOI")
                {
                    ketQuaHopLe = false;
                    ghiChu = "CẢNH BÁO: Văn bằng này đã bị đơn vị cấp thu hồi hoặc hủy bỏ hiệu lực.";
                }
                else
                {
                    // Xác thực lại chữ ký số bằng Public Key của đơn vị
                    if (vanBang.KhoaKySo != null && !string.IsNullOrEmpty(vanBang.KhoaKySo.PublicKeyText))
                    {
                        chuKySoHopLe = _signatureService.VerifySignature(
                            fileBytes,
                            vanBang.ChuKySo,
                            vanBang.KhoaKySo.PublicKeyText
                        );
                    }

                    if (chuKySoHopLe)
                    {
                        ketQuaHopLe = true;
                        ghiChu = "CHÍNH XÁC: Văn bằng hợp lệ, toàn vẹn dữ liệu và chữ ký số chính chủ.";
                    }
                    else
                    {
                        ketQuaHopLe = false;
                        ghiChu = "CẢNH BÁO: Mã băm trùng khớp nhưng chữ ký số không hợp lệ hoặc đã bị can thiệp.";
                    }
                }
            }
            else
            {
                ketQuaHopLe = false;
                ghiChu = "CẢNH BÁO: Tệp tin không tồn tại trong hệ thống hoặc nội dung văn bằng đã bị chỉnh sửa, làm giả.";
            }

            // 4. Lưu lịch sử vào bảng LichSuKiemTra
            var lichSu = new LichSuKiemTra
            {
                TenFileTaiLen = tenFile,
                MaBamFileTaiLen = maBamFile,
                KetQuaHopLe = ketQuaHopLe,
                GhiChu = ghiChu,
                IPNguoiKiemTra = ipClient,
                ThoiGianKiemTra = DateTime.UtcNow
            };

            _context.LichSuKiemTras.Add(lichSu);
            await _context.SaveChangesAsync();

            // 5. Chuẩn bị kết quả phản hồi
            var ketQua = new
            {
                KetQuaHopLe = ketQuaHopLe,
                ThongBao = ghiChu,
                MaBamFileTaiLen = maBamFile,
                TenFile = tenFile,
                ThoiGianKiemTra = lichSu.ThoiGianKiemTra,
                ChuKySoHopLe = chuKySoHopLe,
                ThongTinVanBang = vanBang != null ? new
                {
                    MaVanBang = vanBang.MaVanBang,
                    TenVanBang = vanBang.TenVanBang,
                    SoHieu = vanBang.SoHieu,
                    NgayCap = vanBang.NgayCap,
                    TrangThai = vanBang.TrangThai,
                    HoTenNguoiNhan = vanBang.NguoiNhan?.HoTen ?? string.Empty,
                    SoCCCD = vanBang.NguoiNhan?.SoCCCD ?? string.Empty,
                    NgaySinh = vanBang.NguoiNhan?.NgaySinh ?? DateTime.MinValue,
                    TenDonViPhatHanh = vanBang.DonViPhatHanh?.TenDonVi ?? string.Empty,
                    MaCodeDonVi = vanBang.DonViPhatHanh?.MaCode ?? string.Empty,
                    ChuKySo = vanBang.ChuKySo,
                    DuongDanFileGoc = vanBang.DuongDanFileDaKy
                } : null
            };

            return Ok(ketQua);
        }

        /// <summary>
        /// Tra cứu tính hợp lệ nhanh qua chuỗi mã băm SHA-256 (64 ký tự)
        /// </summary>
        [HttpPost("kiem-tra-theo-hash")]
        public async Task<IActionResult> KiemTraTheoHash([FromBody] JsonElement dto)
        {
            string maBam = dto.GetProperty("maBamSHA256").GetString()?.Trim().ToLowerInvariant() ?? "";
            string tenFile = dto.TryGetProperty("tenFile", out var tf) ? tf.GetString() ?? "KiemTraTheoChuoiHash" : "KiemTraTheoChuoiHash";
            string? ipClient = HttpContext.Connection.RemoteIpAddress?.ToString();

            var vanBang = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .FirstOrDefaultAsync(v => v.MaBamSHA256 == maBam);

            bool ketQuaHopLe = false;
            string ghiChu = string.Empty;

            if (vanBang != null)
            {
                if (vanBang.TrangThai == "THU_HOI")
                {
                    ketQuaHopLe = false;
                    ghiChu = "CẢNH BÁO: Văn bằng đã bị cơ quan cấp bằng thu hồi.";
                }
                else
                {
                    ketQuaHopLe = true;
                    ghiChu = "HỢP LỆ: Tìm thấy văn bằng trùng khớp với mã băm trong hệ thống.";
                }
            }
            else
            {
                ketQuaHopLe = false;
                ghiChu = "KHÔNG TÌM THẤY: Mã băm không tồn tại trên hệ thống.";
            }

            // Ghi nhận vào bảng LichSuKiemTra
            var lichSu = new LichSuKiemTra
            {
                TenFileTaiLen = tenFile,
                MaBamFileTaiLen = maBam,
                KetQuaHopLe = ketQuaHopLe,
                GhiChu = ghiChu,
                IPNguoiKiemTra = ipClient,
                ThoiGianKiemTra = DateTime.UtcNow
            };

            _context.LichSuKiemTras.Add(lichSu);
            await _context.SaveChangesAsync();

            var ketQua = new 
            {
                KetQuaHopLe = ketQuaHopLe,
                ThongBao = ghiChu,
                MaBamFileTaiLen = maBam,
                TenFile = tenFile,
                ThoiGianKiemTra = lichSu.ThoiGianKiemTra,
                ChuKySoHopLe = ketQuaHopLe,
                ThongTinVanBang = vanBang != null ? new 
                {
                    MaVanBang = vanBang.MaVanBang,
                    TenVanBang = vanBang.TenVanBang,
                    SoHieu = vanBang.SoHieu,
                    NgayCap = vanBang.NgayCap,
                    TrangThai = vanBang.TrangThai,
                    HoTenNguoiNhan = vanBang.NguoiNhan?.HoTen ?? string.Empty,
                    SoCCCD = vanBang.NguoiNhan?.SoCCCD ?? string.Empty,
                    NgaySinh = vanBang.NguoiNhan?.NgaySinh ?? DateTime.MinValue,
                    TenDonViPhatHanh = vanBang.DonViPhatHanh?.TenDonVi ?? string.Empty,
                    MaCodeDonVi = vanBang.DonViPhatHanh?.MaCode ?? string.Empty,
                    ChuKySo = vanBang.ChuKySo,
                    DuongDanFileGoc = vanBang.DuongDanFileDaKy
                } : null
            };

            return Ok(ketQua);
        }

        /// <summary>
        /// Xem lịch sử kiểm tra văn bằng gần nhất
        /// </summary>
        [HttpGet("lich-su")]
        public async Task<ActionResult<IEnumerable<LichSuKiemTra>>> GetLichSu([FromQuery] int soLuong = 20)
        {
            if (soLuong <= 0 || soLuong > 100) soLuong = 20;

            var danhSach = await _context.LichSuKiemTras
                .OrderByDescending(l => l.ThoiGianKiemTra)
                .Take(soLuong)
                .ToListAsync();

            return Ok(danhSach);
        }
    }
}
