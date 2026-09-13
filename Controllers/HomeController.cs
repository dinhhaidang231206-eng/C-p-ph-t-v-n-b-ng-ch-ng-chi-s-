using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;
using HeThongVanBangSo.Services;

namespace HeThongVanBangSo.Controllers
{
    public class HomeController : Controller
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ISignatureService _signatureService;

        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        public HomeController(HeThongVanBangDbContext context, ISignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: /Home/TraCuu
        public IActionResult TraCuu()
        {
            return View();
        }

        // POST: /Home/TraCuu — Kiểm tra qua file upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TraCuuByFile(IFormFile fileVanBang)
        {
            if (fileVanBang == null || fileVanBang.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn tệp tin văn bằng để kiểm tra.";
                return RedirectToAction(nameof(TraCuu));
            }

            if (fileVanBang.Length > MaxFileSize)
            {
                TempData["ErrorMessage"] = "Kích thước tệp tin vượt quá giới hạn 10 MB.";
                return RedirectToAction(nameof(TraCuu));
            }

            // Đọc file và tính hash
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await fileVanBang.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            string maBamFile = _signatureService.ComputeSha256Hash(fileBytes);
            string tenFile = Path.GetFileName(fileVanBang.FileName);
            string? ipClient = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Đối chiếu trong DB
            var vanBang = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .Include(v => v.KhoaKySo)
                .FirstOrDefaultAsync(v => v.MaBamSHA256 == maBamFile);

            bool ketQuaHopLe = false;
            bool chuKySoHopLe = false;
            string ghiChu;

            if (vanBang != null)
            {
                if (vanBang.TrangThai == "THU_HOI")
                {
                    ghiChu = "CẢNH BÁO: Văn bằng này đã bị đơn vị cấp thu hồi hoặc hủy bỏ hiệu lực.";
                }
                else
                {
                    // Xác thực chữ ký số
                    if (vanBang.KhoaKySo != null && !string.IsNullOrEmpty(vanBang.KhoaKySo.PublicKeyText))
                    {
                        chuKySoHopLe = _signatureService.VerifySignature(
                            fileBytes, vanBang.ChuKySo, vanBang.KhoaKySo.PublicKeyText);
                    }

                    if (chuKySoHopLe)
                    {
                        ketQuaHopLe = true;
                        ghiChu = "CHÍNH XÁC: Văn bằng hợp lệ, toàn vẹn dữ liệu và chữ ký số chính chủ.";
                    }
                    else
                    {
                        ghiChu = "CẢNH BÁO: Mã băm trùng khớp nhưng chữ ký số không hợp lệ hoặc đã bị can thiệp.";
                    }
                }
            }
            else
            {
                ghiChu = "CẢNH BÁO: Tệp tin không tồn tại trong hệ thống hoặc nội dung đã bị chỉnh sửa, làm giả.";
            }

            // Lưu lịch sử
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

            // Trả kết quả về View
            ViewBag.KetQuaHopLe = ketQuaHopLe;
            ViewBag.ChuKySoHopLe = chuKySoHopLe;
            ViewBag.GhiChu = ghiChu;
            ViewBag.MaBamFile = maBamFile;
            ViewBag.TenFile = tenFile;
            ViewBag.VanBang = vanBang;
            ViewBag.DaTraCuu = true;

            return View(nameof(TraCuu));
        }

        // POST: /Home/TraCuuByHash — Kiểm tra qua chuỗi hash SHA-256
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TraCuuByHash(string maBamSHA256)
        {
            if (string.IsNullOrWhiteSpace(maBamSHA256))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập mã băm SHA-256 để tra cứu.";
                return RedirectToAction(nameof(TraCuu));
            }

            string maBam = maBamSHA256.Trim().ToLowerInvariant();

            // Validate hash format (64 hex chars)
            if (maBam.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(maBam, "^[0-9a-f]{64}$"))
            {
                TempData["ErrorMessage"] = "Mã băm SHA-256 phải có đúng 64 ký tự hex (0-9, a-f).";
                return RedirectToAction(nameof(TraCuu));
            }

            string? ipClient = HttpContext.Connection.RemoteIpAddress?.ToString();

            var vanBang = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .FirstOrDefaultAsync(v => v.MaBamSHA256 == maBam);

            bool ketQuaHopLe = false;
            string ghiChu;

            if (vanBang != null)
            {
                if (vanBang.TrangThai == "THU_HOI")
                {
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
                ghiChu = "KHÔNG TÌM THẤY: Mã băm không tồn tại trên hệ thống.";
            }

            // Lưu lịch sử
            var lichSu = new LichSuKiemTra
            {
                TenFileTaiLen = "TraCuuTheoHash",
                MaBamFileTaiLen = maBam,
                KetQuaHopLe = ketQuaHopLe,
                GhiChu = ghiChu,
                IPNguoiKiemTra = ipClient,
                ThoiGianKiemTra = DateTime.UtcNow
            };
            _context.LichSuKiemTras.Add(lichSu);
            await _context.SaveChangesAsync();

            ViewBag.KetQuaHopLe = ketQuaHopLe;
            ViewBag.ChuKySoHopLe = ketQuaHopLe;
            ViewBag.GhiChu = ghiChu;
            ViewBag.MaBamFile = maBam;
            ViewBag.TenFile = "Tra cứu theo mã băm";
            ViewBag.VanBang = vanBang;
            ViewBag.DaTraCuu = true;

            return View(nameof(TraCuu));
        }
    }
}
