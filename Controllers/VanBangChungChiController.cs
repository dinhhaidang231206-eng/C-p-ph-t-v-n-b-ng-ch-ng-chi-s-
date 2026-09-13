using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;
using HeThongVanBangSo.Services;

namespace HeThongVanBangSo.Controllers
{
    public class VanBangChungChiController : Controller
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ISignatureService _signatureService;
        private readonly IWebHostEnvironment _env;

        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AllowedExtensions = { ".pdf" };

        public VanBangChungChiController(
            HeThongVanBangDbContext context,
            ISignatureService signatureService,
            IWebHostEnvironment env)
        {
            _context = context;
            _signatureService = signatureService;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var vanBangs = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .OrderByDescending(v => v.NgayTao)
                .ToListAsync();

            return View(vanBangs);
        }

        // GET: /VanBangChungChi/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.DanhSachDonVi = await _context.DonViPhatHanhs
                .Where(d => d.TrangThaiHoatDong)
                .OrderBy(d => d.TenDonVi)
                .ToListAsync();

            ViewBag.DanhSachNguoiNhan = await _context.NguoiNhans
                .OrderBy(n => n.HoTen)
                .ToListAsync();

            return View();
        }

        // POST: /VanBangChungChi/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int maDonVi, long maNguoiNhan, string tenVanBang, string soHieu, DateTime ngayCap, IFormFile fileVanBang)
        {
            // Load lại danh sách cho dropdown khi cần trả về View
            async Task LoadViewBags()
            {
                ViewBag.DanhSachDonVi = await _context.DonViPhatHanhs
                    .Where(d => d.TrangThaiHoatDong)
                    .OrderBy(d => d.TenDonVi)
                    .ToListAsync();
                ViewBag.DanhSachNguoiNhan = await _context.NguoiNhans
                    .OrderBy(n => n.HoTen)
                    .ToListAsync();
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(tenVanBang) || string.IsNullOrWhiteSpace(soHieu))
            {
                TempData["ErrorMessage"] = "Tên văn bằng và Số hiệu không được để trống.";
                await LoadViewBags();
                return View();
            }

            // Validate file
            if (fileVanBang == null || fileVanBang.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng đính kèm tệp tin văn bằng (PDF).";
                await LoadViewBags();
                return View();
            }

            if (fileVanBang.Length > MaxFileSize)
            {
                TempData["ErrorMessage"] = "Kích thước tệp tin vượt quá giới hạn 10 MB.";
                await LoadViewBags();
                return View();
            }

            string ext = Path.GetExtension(fileVanBang.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Chỉ chấp nhận tệp tin định dạng PDF.";
                await LoadViewBags();
                return View();
            }

            // 1. Kiểm tra đơn vị
            var donVi = await _context.DonViPhatHanhs.FindAsync(maDonVi);
            if (donVi == null || !donVi.TrangThaiHoatDong)
            {
                TempData["ErrorMessage"] = "Đơn vị phát hành không tồn tại hoặc đã ngừng hoạt động.";
                await LoadViewBags();
                return View();
            }

            // 2. Kiểm tra người nhận
            var nguoiNhan = await _context.NguoiNhans.FindAsync(maNguoiNhan);
            if (nguoiNhan == null)
            {
                TempData["ErrorMessage"] = "Người nhận văn bằng không tồn tại.";
                await LoadViewBags();
                return View();
            }

            // 3. Kiểm tra trùng số hiệu
            bool isSoHieuExist = await _context.VanBangChungChis.AnyAsync(v => v.SoHieu == soHieu.Trim());
            if (isSoHieuExist)
            {
                TempData["ErrorMessage"] = $"Số hiệu văn bằng '{soHieu}' đã tồn tại.";
                await LoadViewBags();
                return View();
            }

            // 4. Lấy khóa ký số hợp lệ
            var khoaKy = await _context.KhoaKySos
                .Where(k => k.MaDonVi == maDonVi && k.TrangThai && k.NgayHetHan > DateTime.UtcNow)
                .OrderByDescending(k => k.MaKhoa)
                .FirstOrDefaultAsync();

            if (khoaKy == null)
            {
                TempData["ErrorMessage"] = "Đơn vị chưa có khóa ký số hợp lệ. Hãy tạo khóa mới trước.";
                await LoadViewBags();
                return View();
            }

            // 5. Đọc file
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await fileVanBang.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // 6. Tính mã băm SHA-256 và ký số RSA
            string maBamSha256 = _signatureService.ComputeSha256Hash(fileBytes);
            string chuKySo = _signatureService.SignData(fileBytes, khoaKy.PrivateKeyMaHoa);

            // 7. Lưu file
            string uploadsFolder = Path.Combine(_env.ContentRootPath, "Uploads", "Certificates");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string safeFileName = $"{soHieu.Trim()}_{Path.GetFileName(fileVanBang.FileName)}";
            string filePath = Path.Combine(uploadsFolder, safeFileName);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            string duongDanLuuTru = $"/Uploads/Certificates/{safeFileName}";

            // 8. Lưu vào DB
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

            TempData["SuccessMessage"] = $"Đã cấp phát văn bằng '{vanBang.TenVanBang}' (Số hiệu: {vanBang.SoHieu}) thành công! Mã băm SHA-256: {maBamSha256}";
            return RedirectToAction(nameof(Index));
        }

        // GET: /VanBangChungChi/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null) return NotFound();

            var vanBang = await _context.VanBangChungChis
                .Include(v => v.DonViPhatHanh)
                .Include(v => v.NguoiNhan)
                .Include(v => v.KhoaKySo)
                .FirstOrDefaultAsync(v => v.MaVanBang == id);

            if (vanBang == null) return NotFound();

            return View(vanBang);
        }

        // POST: /VanBangChungChi/ThuHoi/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThuHoi(long id, string? lyDo)
        {
            var vanBang = await _context.VanBangChungChis.FindAsync(id);
            if (vanBang == null) return NotFound();

            if (vanBang.TrangThai == "THU_HOI")
            {
                TempData["ErrorMessage"] = "Văn bằng này đã bị thu hồi trước đó.";
                return RedirectToAction(nameof(Index));
            }

            vanBang.TrangThai = "THU_HOI";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Văn bằng số hiệu '{vanBang.SoHieu}' đã bị thu hồi. Lý do: {lyDo ?? "Không nêu rõ"}";
            return RedirectToAction(nameof(Index));
        }
    }
}
