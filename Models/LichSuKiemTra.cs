using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongVanBangSo.Models
{
    [Table("LichSuKiemTra")]
    public class LichSuKiemTra
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MaKiemTra { get; set; }

        [Required]
        [MaxLength(255)]
        public string TenFileTaiLen { get; set; } = string.Empty;

        [Required]
        [StringLength(64, MinimumLength = 64)]
        [Column(TypeName = "char(64)")]
        public string MaBamFileTaiLen { get; set; } = string.Empty;

        public bool KetQuaHopLe { get; set; }

        [MaxLength(255)]
        public string? GhiChu { get; set; }

        [MaxLength(45)]
        public string? IPNguoiKiemTra { get; set; }

        public DateTime ThoiGianKiemTra { get; set; } = DateTime.UtcNow;
    }
}
