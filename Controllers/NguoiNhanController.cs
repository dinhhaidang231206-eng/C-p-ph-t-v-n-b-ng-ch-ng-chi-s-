using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;

namespace HeThongVanBangSo.Controllers
{
    public class NguoiNhanController : Controller
    {
        private readonly HeThongVanBangDbContext _context;

        public NguoiNhanController(HeThongVanBangDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var nguoiNhans = await _context.NguoiNhans
                .OrderByDescending(n => n.MaNguoiNhan)
                .ToListAsync();

            return View(nguoiNhans);
        }

        // GET: /NguoiNhan/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /NguoiNhan/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NguoiNhan nguoiNhan)
        {
            if (!ModelState.IsValid)
            {
                return View(nguoiNhan);
            }

            // Kiểm tra trùng CCCD
            bool isCccdExist = await _context.NguoiNhans.AnyAsync(n => n.SoCCCD == nguoiNhan.SoCCCD.Trim());
            if (isCccdExist)
            {
                ModelState.AddModelError("SoCCCD", $"Số CCCD '{nguoiNhan.SoCCCD}' đã tồn tại trong hệ thống.");
                return View(nguoiNhan);
            }

            nguoiNhan.HoTen = nguoiNhan.HoTen.Trim();
            nguoiNhan.SoCCCD = nguoiNhan.SoCCCD.Trim();
            nguoiNhan.Email = nguoiNhan.Email?.Trim();
            nguoiNhan.NgaySinh = nguoiNhan.NgaySinh.Date;

            _context.NguoiNhans.Add(nguoiNhan);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm người nhận '{nguoiNhan.HoTen}' thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /NguoiNhan/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null) return NotFound();

            var nguoiNhan = await _context.NguoiNhans.FindAsync(id);
            if (nguoiNhan == null) return NotFound();

            return View(nguoiNhan);
        }

        // POST: /NguoiNhan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, NguoiNhan nguoiNhan)
        {
            if (id != nguoiNhan.MaNguoiNhan) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(nguoiNhan);
            }

            // Kiểm tra trùng CCCD với người khác
            bool isCccdDuplicate = await _context.NguoiNhans
                .AnyAsync(n => n.SoCCCD == nguoiNhan.SoCCCD.Trim() && n.MaNguoiNhan != id);
            if (isCccdDuplicate)
            {
                ModelState.AddModelError("SoCCCD", $"Số CCCD '{nguoiNhan.SoCCCD}' đã được sử dụng bởi người khác.");
                return View(nguoiNhan);
            }

            try
            {
                var entity = await _context.NguoiNhans.FindAsync(id);
                if (entity == null) return NotFound();

                entity.HoTen = nguoiNhan.HoTen.Trim();
                entity.SoCCCD = nguoiNhan.SoCCCD.Trim();
                entity.Email = nguoiNhan.Email?.Trim();
                entity.NgaySinh = nguoiNhan.NgaySinh.Date;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật thông tin '{entity.HoTen}' thành công.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.NguoiNhans.AnyAsync(n => n.MaNguoiNhan == id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
