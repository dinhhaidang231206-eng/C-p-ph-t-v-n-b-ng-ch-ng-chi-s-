using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HeThongVanBangSo.Models
{
    [Table("NguoiNhan")]
    public class NguoiNhan
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MaNguoiNhan { get; set; }

        [Required(ErrorMessage = "Họ tên người nhận không được để trống")]
        [MaxLength(150)]
        public string HoTen { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số CCCD/CMND là bắt buộc")]
        [MaxLength(20)]
        public string SoCCCD { get; set; } = string.Empty;

        [MaxLength(100), EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
        [Column(TypeName = "date")]
        public DateTime NgaySinh { get; set; }

        [JsonIgnore]
        public virtual ICollection<VanBangChungChi> DanhSachVanBang { get; set; } = new List<VanBangChungChi>();
    }
}
