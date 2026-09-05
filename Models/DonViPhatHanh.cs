using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HeThongVanBangSo.Models
{
    [Table("DonViPhatHanh")]
    public class DonViPhatHanh
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MaDonVi { get; set; }

        [Required(ErrorMessage = "Tên đơn vị không được để trống")]
        [MaxLength(255)]
        public string TenDonVi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã code đơn vị là bắt buộc")]
        [MaxLength(50)]
        public string MaCode { get; set; } = string.Empty;

        [MaxLength(100), EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string? Email { get; set; }

        public bool TrangThaiHoatDong { get; set; } = true;

        [JsonIgnore]
        public virtual ICollection<KhoaKySo> DanhSachKhoaKySo { get; set; } = new List<KhoaKySo>();

        [JsonIgnore]
        public virtual ICollection<VanBangChungChi> DanhSachVanBang { get; set; } = new List<VanBangChungChi>();
    }
}
