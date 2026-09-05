using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HeThongVanBangSo.Models
{
    [Table("KhoaKySo")]
    public class KhoaKySo
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MaKhoa { get; set; }

        [Required]
        public int MaDonVi { get; set; }

        [Required(ErrorMessage = "Khóa công khai (Public Key) không được để trống")]
        public string PublicKeyText { get; set; } = string.Empty;

        [Required(ErrorMessage = "Khóa bí mật (Private Key) không được để trống")]
        public string PrivateKeyMaHoa { get; set; } = string.Empty;

        [Required]
        public DateTime NgayHetHan { get; set; }

        public bool TrangThai { get; set; } = true;

        [ForeignKey("MaDonVi")]
        public virtual DonViPhatHanh? DonViPhatHanh { get; set; }

        [JsonIgnore]
        public virtual ICollection<VanBangChungChi> DanhSachVanBang { get; set; } = new List<VanBangChungChi>();
    }
}
