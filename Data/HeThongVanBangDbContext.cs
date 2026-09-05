using Microsoft.EntityFrameworkCore;
using HeThongVanBangSo.Models;

namespace HeThongVanBangSo.Data
{
    public class HeThongVanBangDbContext : DbContext
    {
        public HeThongVanBangDbContext(DbContextOptions<HeThongVanBangDbContext> options)
            : base(options)
        {
        }

        public DbSet<DonViPhatHanh> DonViPhatHanhs { get; set; } = null!;
        public DbSet<NguoiNhan> NguoiNhans { get; set; } = null!;
        public DbSet<KhoaKySo> KhoaKySos { get; set; } = null!;
        public DbSet<VanBangChungChi> VanBangChungChis { get; set; } = null!;
        public DbSet<LichSuKiemTra> LichSuKiemTras { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Cấu hình bảng DonViPhatHanh
            modelBuilder.Entity<DonViPhatHanh>(entity =>
            {
                entity.HasIndex(e => e.MaCode).IsUnique();
            });

            // 2. Cấu hình bảng NguoiNhan
            modelBuilder.Entity<NguoiNhan>(entity =>
            {
                entity.HasIndex(e => e.SoCCCD).IsUnique();
            });

            // 3. Cấu hình bảng KhoaKySo
            modelBuilder.Entity<KhoaKySo>(entity =>
            {
                entity.HasOne(k => k.DonViPhatHanh)
                      .WithMany(d => d.DanhSachKhoaKySo)
                      .HasForeignKey(k => k.MaDonVi)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 4. Cấu hình bảng VanBangChungChi
            modelBuilder.Entity<VanBangChungChi>(entity =>
            {
                entity.HasIndex(e => e.SoHieu).IsUnique();
                entity.HasIndex(e => e.MaBamSHA256).HasDatabaseName("IDX_VanBang_MaBam");

                entity.HasOne(v => v.DonViPhatHanh)
                      .WithMany(d => d.DanhSachVanBang)
                      .HasForeignKey(v => v.MaDonVi)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.NguoiNhan)
                      .WithMany(n => n.DanhSachVanBang)
                      .HasForeignKey(v => v.MaNguoiNhan)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.KhoaKySo)
                      .WithMany(k => k.DanhSachVanBang)
                      .HasForeignKey(v => v.MaKhoa)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 5. Cấu hình bảng LichSuKiemTra
            modelBuilder.Entity<LichSuKiemTra>(entity =>
            {
                entity.HasIndex(e => e.MaBamFileTaiLen);
            });
        }
    }
}
