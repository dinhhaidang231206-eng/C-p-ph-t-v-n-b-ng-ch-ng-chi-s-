using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;
using HeThongVanBangSo.Services;

namespace HeThongVanBangSo.Controllers
{
    public class KhoaKySoController : Controller
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ISignatureService _signatureService;

        public KhoaKySoController(HeThongVanBangDbContext context, ISignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }

        public async Task<IActionResult> Index()
        {
            var khoas = await _context.KhoaKySos
                .Include(k => k.DonViPhatHanh)
                .OrderByDescending(k => k.MaKhoa)
                .ToListAsync();

            return View(khoas);
        }

        // GET: /KhoaKySo/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.DanhSachDonVi = await _context.DonViPhatHanhs
                .Where(d => d.TrangThaiHoatDong)
                .OrderBy(d => d.TenDonVi)
                .ToListAsync();

            return View();
        }

        // POST: /KhoaKySo/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int maDonVi, DateTime ngayHetHan)
        {
            var donVi = await _context.DonViPhatHanhs.FindAsync(maDonVi);
            if (donVi == null || !donVi.TrangThaiHoatDong)
            {
                TempData["ErrorMessage"] = "Đơn vị phát hành không tồn tại hoặc đã ngừng hoạt động.";
                return RedirectToAction(nameof(Create));
            }

            if (ngayHetHan <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Ngày hết hạn phải lớn hơn ngày hiện tại.";
                return RedirectToAction(nameof(Create));
            }

            // Sinh cặp khóa RSA 2048-bit
            var (publicKey, privateKey) = _signatureService.GenerateRsaKeyPair(2048);

            var khoaKySo = new KhoaKySo
            {
                MaDonVi = maDonVi,
                PublicKeyText = publicKey,
                PrivateKeyMaHoa = privateKey,
                NgayHetHan = ngayHetHan,
                TrangThai = true
            };

            _context.KhoaKySos.Add(khoaKySo);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã sinh khóa RSA 2048-bit thành công cho đơn vị '{donVi.TenDonVi}' (Mã khóa: {khoaKySo.MaKhoa}).";
            return RedirectToAction(nameof(Index));
        }

        // POST: /KhoaKySo/VoHieuHoa/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoHieuHoa(int id)
        {
            var khoa = await _context.KhoaKySos.FindAsync(id);
            if (khoa == null) return NotFound();

            khoa.TrangThai = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Khóa ký số #{id} đã được vô hiệu hóa thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
