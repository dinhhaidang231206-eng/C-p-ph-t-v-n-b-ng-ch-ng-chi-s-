using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Models;
using HeThongVanBangSo.Services;

namespace HeThongVanBangSo.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class KhoaKySoController : ControllerBase
    {
        private readonly HeThongVanBangDbContext _context;
        private readonly ISignatureService _signatureService;

        public KhoaKySoController(HeThongVanBangDbContext context, ISignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }

        /// <summary>
        /// Tự động sinh cặp khóa RSA 2048-bit cho đơn vị phát hành (Public Key và Private Key)
        /// </summary>
        [HttpPost("sinh-khoa")]
        public async Task<ActionResult<KhoaKySo>> SinhKhoaKySo([FromBody] KhoaKySo dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var donVi = await _context.DonViPhatHanhs.FindAsync(dto.MaDonVi);
            if (donVi == null)
            {
                return NotFound(new { message = $"Không tìm thấy đơn vị có mã {dto.MaDonVi}" });
            }

            // Sinh cặp khóa RSA 2048-bit
            var (publicKey, privateKey) = _signatureService.GenerateRsaKeyPair(2048);

            var khoaKySo = new KhoaKySo
            {
                MaDonVi = dto.MaDonVi,
                PublicKeyText = publicKey,
                PrivateKeyMaHoa = privateKey, // Trong thực tế lưu vào HSM/KMS hoặc mã hóa đối xứng
                NgayHetHan = dto.NgayHetHan,
                TrangThai = true
            };

            _context.KhoaKySos.Add(khoaKySo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetKhoaById), new { id = khoaKySo.MaKhoa }, khoaKySo);
        }

        /// <summary>
        /// Lấy thông tin khóa ký số theo Mã khóa (chỉ trả về Public Key để bảo mật)
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetKhoaById(int id)
        {
            var khoa = await _context.KhoaKySos
                .Include(k => k.DonViPhatHanh)
                .FirstOrDefaultAsync(k => k.MaKhoa == id);

            if (khoa == null)
            {
                return NotFound(new { message = $"Không tìm thấy khóa có mã {id}" });
            }

            // Bảo mật: Không trả về Private Key qua API
            return Ok(new
            {
                khoa.MaKhoa,
                khoa.MaDonVi,
                khoa.PublicKeyText,
                khoa.NgayHetHan,
                khoa.TrangThai,
                DonViPhatHanh = khoa.DonViPhatHanh != null ? new { khoa.DonViPhatHanh.MaDonVi, khoa.DonViPhatHanh.TenDonVi, khoa.DonViPhatHanh.MaCode } : null
            });
        }

        /// <summary>
        /// Lấy danh sách khóa ký số của một đơn vị phát hành
        /// </summary>
        [HttpGet("donvi/{maDonVi:int}")]
        public async Task<IActionResult> GetKhoaTheoDonVi(int maDonVi)
        {
            var danhSach = await _context.KhoaKySos
                .Where(k => k.MaDonVi == maDonVi)
                .Include(k => k.DonViPhatHanh)
                .OrderByDescending(k => k.MaKhoa)
                .Select(k => new
                {
                    k.MaKhoa,
                    k.MaDonVi,
                    k.PublicKeyText,
                    k.NgayHetHan,
                    k.TrangThai,
                    TenDonVi = k.DonViPhatHanh != null ? k.DonViPhatHanh.TenDonVi : null
                })
                .ToListAsync();

            return Ok(danhSach);
        }

        /// <summary>
        /// Vô hiệu hóa một khóa ký số
        /// </summary>
        [HttpPut("{id:int}/vo-hieu-hoa")]
        public async Task<IActionResult> VoHieuHoaKhoa(int id)
        {
            var khoa = await _context.KhoaKySos.FindAsync(id);
            if (khoa == null)
            {
                return NotFound(new { message = $"Không tìm thấy khóa ký số {id}" });
            }

            khoa.TrangThai = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Khóa ký số {id} đã được vô hiệu hóa thành công." });
        }
    }
}
