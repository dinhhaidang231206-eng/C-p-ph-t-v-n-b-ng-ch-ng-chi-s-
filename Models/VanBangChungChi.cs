using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongVanBangSo.Models
{
    [Table("VanBangChungChi")]
    public class VanBangChungChi
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MaVanBang { get; set; }

        [Required]
        public int MaDonVi { get; set; }

        [Required]
        public long MaNguoiNhan { get; set; }

        [Required]
        public int MaKhoa { get; set; }

        [Required(ErrorMessage = "Tên văn bằng không được để trống")]
        [MaxLength(255)]
        public string TenVanBang { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số hiệu văn bằng là bắt buộc")]
        [MaxLength(50)]
        public string SoHieu { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string DuongDanFileDaKy { get; set; } = string.Empty;

        [Required]
        public string ChuKySo { get; set; } = string.Empty;

        [Required]
        [StringLength(64, MinimumLength = 64, ErrorMessage = "Mã băm SHA-256 phải có đúng 64 ký tự hex")]
        [Column(TypeName = "char(64)")]
        public string MaBamSHA256 { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "date")]
        public DateTime NgayCap { get; set; }

        [MaxLength(20)]
        public string TrangThai { get; set; } = "HOP_LE";

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        [ForeignKey("MaDonVi")]
        public virtual DonViPhatHanh? DonViPhatHanh { get; set; }

        [ForeignKey("MaNguoiNhan")]
        public virtual NguoiNhan? NguoiNhan { get; set; }

        [ForeignKey("MaKhoa")]
        public virtual KhoaKySo? KhoaKySo { get; set; }
    }
}
