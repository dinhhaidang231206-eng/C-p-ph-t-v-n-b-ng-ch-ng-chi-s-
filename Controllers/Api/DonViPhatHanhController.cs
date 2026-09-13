using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;

namespace HeThongVanBangSo.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class DonViPhatHanhController : ControllerBase
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ILogger<DonViPhatHanhController> _logger;

        public DonViPhatHanhController(HeThongVanBangDbContext context, ILogger<DonViPhatHanhController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả đơn vị phát hành
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DonViPhatHanh>>> GetDanhSachDonVi([FromQuery] bool? chiHoatDong = true)
        {
            var query = _context.DonViPhatHanhs.AsQueryable();
            if (chiHoatDong.HasValue && chiHoatDong.Value)
            {
                query = query.Where(d => d.TrangThaiHoatDong);
            }

            return await query.OrderBy(d => d.TenDonVi).ToListAsync();
        }

        /// <summary>
        /// Lấy thông tin chi tiết một đơn vị phát hành theo Mã
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<DonViPhatHanh>> GetDonViById(int id)
        {
            var donVi = await _context.DonViPhatHanhs.FindAsync(id);
            if (donVi == null)
            {
                return NotFound(new { message = $"Không tìm thấy đơn vị có mã {id}" });
            }

            return donVi;
        }

        /// <summary>
        /// Tạo mới một đơn vị phát hành văn bằng
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DonViPhatHanh>> TaoDonVi([FromBody] DonViPhatHanh dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra trùng MaCode
            bool isCodeExist = await _context.DonViPhatHanhs.AnyAsync(d => d.MaCode == dto.MaCode);
            if (isCodeExist)
            {
                return Conflict(new { message = $"Mã code '{dto.MaCode}' đã tồn tại trong hệ thống." });
            }

            var donVi = new DonViPhatHanh
            {
                TenDonVi = dto.TenDonVi,
                MaCode = dto.MaCode,
                Email = dto.Email,
                TrangThaiHoatDong = dto.TrangThaiHoatDong
            };

            _context.DonViPhatHanhs.Add(donVi);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDonViById), new { id = donVi.MaDonVi }, donVi);
        }

        /// <summary>
        /// Cập nhật thông tin đơn vị phát hành
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> CapNhatDonVi(int id, [FromBody] DonViPhatHanh dto)
        {
            var donVi = await _context.DonViPhatHanhs.FindAsync(id);
            if (donVi == null)
            {
                return NotFound(new { message = $"Không tìm thấy đơn vị có mã {id}" });
            }

            // Kiểm tra trùng MaCode với đơn vị khác
            bool isCodeDuplicate = await _context.DonViPhatHanhs.AnyAsync(d => d.MaCode == dto.MaCode && d.MaDonVi != id);
            if (isCodeDuplicate)
            {
                return Conflict(new { message = $"Mã code '{dto.MaCode}' đã được sử dụng bởi đơn vị khác." });
            }

            donVi.TenDonVi = dto.TenDonVi;
            donVi.MaCode = dto.MaCode;
            donVi.Email = dto.Email;
            donVi.TrangThaiHoatDong = dto.TrangThaiHoatDong;

            await _context.SaveChangesAsync();
            return Ok(donVi);
        }

        /// <summary>
        /// Khóa / Tạm dừng hoạt động đơn vị
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> KhoaDonVi(int id)
        {
            var donVi = await _context.DonViPhatHanhs.FindAsync(id);
            if (donVi == null)
            {
                return NotFound(new { message = $"Không tìm thấy đơn vị có mã {id}" });
            }

            donVi.TrangThaiHoatDong = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đơn vị '{donVi.TenDonVi}' đã được chuyển sang trạng thái ngưng hoạt động." });
        }
    }
}
