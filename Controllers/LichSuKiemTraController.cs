using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;

namespace HeThongVanBangSo.Controllers
{
    public class LichSuKiemTraController : Controller
    {
        private readonly HeThongVanBangDbContext _context;

        public LichSuKiemTraController(HeThongVanBangDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var lichSu = await _context.LichSuKiemTras
                .OrderByDescending(l => l.ThoiGianKiemTra)
                .ToListAsync();

            return View(lichSu);
        }
    }
}
