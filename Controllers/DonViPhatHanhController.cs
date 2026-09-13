using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;

namespace HeThongVanBangSo.Controllers
{
    public class DonViPhatHanhController : Controller
    {
        private readonly HeThongVanBangDbContext _context;

        public DonViPhatHanhController(HeThongVanBangDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var donVis = await _context.DonViPhatHanhs
                .OrderByDescending(d => d.MaDonVi)
                .ToListAsync();

            return View(donVis);
        }

        // GET: /DonViPhatHanh/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /DonViPhatHanh/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DonViPhatHanh donVi)
        {
            if (!ModelState.IsValid)
            {
                return View(donVi);
            }

            // Kiểm tra trùng MaCode
            bool isCodeExist = await _context.DonViPhatHanhs.AnyAsync(d => d.MaCode == donVi.MaCode.Trim());
            if (isCodeExist)
            {
                ModelState.AddModelError("MaCode", $"Mã code '{donVi.MaCode}' đã tồn tại trong hệ thống.");
                return View(donVi);
            }

            donVi.MaCode = donVi.MaCode.Trim();
            donVi.TenDonVi = donVi.TenDonVi.Trim();
            donVi.Email = donVi.Email?.Trim();

            _context.DonViPhatHanhs.Add(donVi);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm đơn vị '{donVi.TenDonVi}' thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /DonViPhatHanh/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var donVi = await _context.DonViPhatHanhs.FindAsync(id);
            if (donVi == null) return NotFound();

            return View(donVi);
        }

        // POST: /DonViPhatHanh/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DonViPhatHanh donVi)
        {
            if (id != donVi.MaDonVi) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(donVi);
            }

            // Kiểm tra trùng MaCode với đơn vị khác
            bool isCodeDuplicate = await _context.DonViPhatHanhs
                .AnyAsync(d => d.MaCode == donVi.MaCode.Trim() && d.MaDonVi != id);
            if (isCodeDuplicate)
            {
                ModelState.AddModelError("MaCode", $"Mã code '{donVi.MaCode}' đã được sử dụng bởi đơn vị khác.");
                return View(donVi);
            }

            try
            {
                var entity = await _context.DonViPhatHanhs.FindAsync(id);
                if (entity == null) return NotFound();

                entity.TenDonVi = donVi.TenDonVi.Trim();
                entity.MaCode = donVi.MaCode.Trim();
                entity.Email = donVi.Email?.Trim();
                entity.TrangThaiHoatDong = donVi.TrangThaiHoatDong;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật đơn vị '{entity.TenDonVi}' thành công.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.DonViPhatHanhs.AnyAsync(d => d.MaDonVi == id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /DonViPhatHanh/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var donVi = await _context.DonViPhatHanhs.FindAsync(id);
            if (donVi == null) return NotFound();

            donVi.TrangThaiHoatDong = !donVi.TrangThaiHoatDong;
            await _context.SaveChangesAsync();

            string trangThai = donVi.TrangThaiHoatDong ? "kích hoạt" : "ngừng hoạt động";
            TempData["SuccessMessage"] = $"Đơn vị '{donVi.TenDonVi}' đã được chuyển sang trạng thái {trangThai}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
