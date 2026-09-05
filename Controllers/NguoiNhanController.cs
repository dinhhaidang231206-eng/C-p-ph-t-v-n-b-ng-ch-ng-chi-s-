using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;

namespace HeThongVanBangSo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NguoiNhanController : ControllerBase
    {
        private readonly HeThongVanBangDbContext _context;

        public NguoiNhanController(HeThongVanBangDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Danh sách người nhận văn bằng (Hỗ trợ lọc theo từ khóa Họ tên hoặc CCCD)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NguoiNhan>>> GetDanhSach([FromQuery] string? tuKhoa)
        {
            var query = _context.NguoiNhans.AsQueryable();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();
                query = query.Where(n => n.HoTen.Contains(tuKhoa) || n.SoCCCD.Contains(tuKhoa));
            }

            return await query.OrderBy(n => n.HoTen).ToListAsync();
        }

        /// <summary>
        /// Lấy chi tiết thông tin người nhận theo Mã
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<ActionResult<NguoiNhan>> GetById(long id)
        {
            var nguoiNhan = await _context.NguoiNhans.FindAsync(id);
            if (nguoiNhan == null)
            {
                return NotFound(new { message = $"Không tìm thấy người nhận có mã {id}" });
            }

            return nguoiNhan;
        }

        /// <summary>
        /// Tra cứu người nhận theo Số CCCD/CMND
        /// </summary>
        [HttpGet("cccd/{soCccd}")]
        public async Task<ActionResult<NguoiNhan>> GetByCccd(string soCccd)
        {
            var nguoiNhan = await _context.NguoiNhans
                .FirstOrDefaultAsync(n => n.SoCCCD == soCccd.Trim());

            if (nguoiNhan == null)
            {
                return NotFound(new { message = $"Không tìm thấy người nhận có CCCD {soCccd}" });
            }

            return nguoiNhan;
        }

        /// <summary>
        /// Thêm mới hồ sơ người nhận văn bằng
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<NguoiNhan>> TaoMoi([FromBody] NguoiNhan dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra trùng CCCD
            bool isCccdExist = await _context.NguoiNhans.AnyAsync(n => n.SoCCCD == dto.SoCCCD.Trim());
            if (isCccdExist)
            {
                return Conflict(new { message = $"Số CCCD '{dto.SoCCCD}' đã tồn tại trong cơ sở dữ liệu." });
            }

            var nguoiNhan = new NguoiNhan
            {
                HoTen = dto.HoTen.Trim(),
                SoCCCD = dto.SoCCCD.Trim(),
                Email = dto.Email?.Trim(),
                NgaySinh = dto.NgaySinh.Date
            };

            _context.NguoiNhans.Add(nguoiNhan);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = nguoiNhan.MaNguoiNhan }, nguoiNhan);
        }

        /// <summary>
        /// Lấy toàn bộ danh sách văn bằng đã được cấp của người nhận
        /// </summary>
        [HttpGet("{id:long}/van-bang")]
        public async Task<IActionResult> GetVanBangCuaNguoiNhan(long id)
        {
            var nguoiNhan = await _context.NguoiNhans.FindAsync(id);
            if (nguoiNhan == null)
            {
                return NotFound(new { message = $"Không tìm thấy người nhận có mã {id}" });
            }

            var danhSachVanBang = await _context.VanBangChungChis
                .Where(v => v.MaNguoiNhan == id)
                .Include(v => v.DonViPhatHanh)
                .Select(v => new
                {
                    v.MaVanBang,
                    v.TenVanBang,
                    v.SoHieu,
                    v.NgayCap,
                    v.TrangThai,
                    v.MaBamSHA256,
                    TenDonVi = v.DonViPhatHanh != null ? v.DonViPhatHanh.TenDonVi : string.Empty
                })
                .ToListAsync();

            return Ok(new
            {
                NguoiNhan = new { nguoiNhan.MaNguoiNhan, nguoiNhan.HoTen, nguoiNhan.SoCCCD },
                TongSoVanBang = danhSachVanBang.Count,
                DanhSachVanBang = danhSachVanBang
            });
        }
    }
}
