IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'HeThongVanBangSo')
BEGIN
    CREATE DATABASE HeThongVanBangSo;
END
GO
USE HeThongVanBangSo;
GO
-- Bảng Đơn vị phát hành (Nơi cấp phát: Bộ, Sở, Trường Đại học, Trung tâm...)
IF OBJECT_ID('dbo.DonViPhatHanh', 'U') IS NULL
BEGIN
    CREATE TABLE DonViPhatHanh (
        MaDonVi INT IDENTITY(1,1) PRIMARY KEY,
        TenDonVi NVARCHAR(255) NOT NULL,
        MaCode VARCHAR(50) NOT NULL UNIQUE,
        Email VARCHAR(100) NULL,
        TrangThaiHoatDong BIT DEFAULT 1
    );
END
GO
-- Bảng Người nhận (Người được cấp chứng chỉ/văn bằng số)
IF OBJECT_ID('dbo.NguoiNhan', 'U') IS NULL
BEGIN
    CREATE TABLE NguoiNhan (
        MaNguoiNhan BIGINT IDENTITY(1,1) PRIMARY KEY,
        HoTen NVARCHAR(150) NOT NULL,
        SoCCCD VARCHAR(20) NOT NULL UNIQUE,
        Email VARCHAR(100) NULL,
        NgaySinh DATE NOT NULL
    );
END
GO
-- Bảng Khóa Ký Số (Lưu Private Key mã hóa & Public Key của từng đơn vị)
IF OBJECT_ID('dbo.KhoaKySo', 'U') IS NULL
BEGIN
    CREATE TABLE KhoaKySo (
        MaKhoa INT IDENTITY(1,1) PRIMARY KEY,
        MaDonVi INT NOT NULL,
        PublicKeyText VARCHAR(MAX) NOT NULL,
        PrivateKeyMaHoa VARCHAR(MAX) NOT NULL,   
        NgayHetHan DATETIME2 NOT NULL,
        TrangThai BIT DEFAULT 1,
        CONSTRAINT FK_Khoa_DonVi FOREIGN KEY (MaDonVi) 
            REFERENCES DonViPhatHanh(MaDonVi) ON DELETE NO ACTION
    );
END
GO
-- Bảng Văn Bằng Chứng Chỉ (Lưu tệp tin đã ký số và mã băm SHA-256)
IF OBJECT_ID('dbo.VanBangChungChi', 'U') IS NULL
BEGIN
    CREATE TABLE VanBangChungChi (
        MaVanBang BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaDonVi INT NOT NULL,
        MaNguoiNhan BIGINT NOT NULL,
        MaKhoa INT NOT NULL,                     
        TenVanBang NVARCHAR(255) NOT NULL,       
        SoHieu VARCHAR(50) NOT NULL UNIQUE,      
        DuongDanFileDaKy NVARCHAR(500) NOT NULL, 
        ChuKySo VARCHAR(MAX) NOT NULL,           
        MaBamSHA256 CHAR(64) NOT NULL,           
        NgayCap DATE NOT NULL,
        TrangThai VARCHAR(20) DEFAULT 'HOP_LE',  
        NgayTao DATETIME2 DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT FK_VanBang_DonVi FOREIGN KEY (MaDonVi) 
            REFERENCES DonViPhatHanh(MaDonVi) ON DELETE NO ACTION,
        CONSTRAINT FK_VanBang_NguoiNhan FOREIGN KEY (MaNguoiNhan) 
            REFERENCES NguoiNhan(MaNguoiNhan) ON DELETE NO ACTION,
        CONSTRAINT FK_VanBang_Khoa FOREIGN KEY (MaKhoa) 
            REFERENCES KhoaKySo(MaKhoa) ON DELETE NO ACTION
    );
END
GO
-- Bảng Lịch sử Kiểm tra (Lưu kết quả khi người dùng tải tệp lên đối chiếu)
IF OBJECT_ID('dbo.LichSuKiemTra', 'U') IS NULL
BEGIN
    CREATE TABLE LichSuKiemTra (
        MaKiemTra BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenFileTaiLen NVARCHAR(255) NOT NULL,
        MaBamFileTaiLen CHAR(64) NOT NULL,       
        KetQuaHopLe BIT NOT NULL,                
        GhiChu NVARCHAR(255) NULL,               
        IPNguoiKiemTra VARCHAR(45) NULL,         
        ThoiGianKiemTra DATETIME2 DEFAULT CURRENT_TIMESTAMP
    );
END
GO
IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IDX_VanBang_MaBam')
BEGIN
    CREATE INDEX IDX_VanBang_MaBam ON VanBangChungChi(MaBamSHA256);
END
GO
IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IDX_VanBang_SoHieu')
BEGIN
    CREATE INDEX IDX_VanBang_SoHieu ON VanBangChungChi(SoHieu);
END
GO
IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IDX_NguoiNhan_SoCCCD')
BEGIN
    CREATE INDEX IDX_NguoiNhan_SoCCCD ON NguoiNhan(SoCCCD);
END
GO
CREATE OR ALTER PROCEDURE sp_KiemTraVanBangByHash
    @MaBam CHAR(64),
    @TenFile NVARCHAR(255),
    @IP VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @HopLe BIT = 0;
    DECLARE @GhiChu NVARCHAR(255) = N'Tệp tin không tồn tại trong hệ thống hoặc đã bị chỉnh sửa nội dung.';
    DECLARE @MaVanBang BIGINT = NULL;
    SELECT TOP 1 
        @MaVanBang = vb.MaVanBang,
        @HopLe = CASE WHEN vb.TrangThai = 'HOP_LE' THEN 1 ELSE 0 END,
        @GhiChu = CASE 
            WHEN vb.TrangThai = 'HOP_LE' THEN N'Văn bằng hợp lệ, chữ ký số và mã băm toàn vẹn.'
            ELSE N'Văn bằng đã bị thu hồi/hủy hiệu lực.'
        END
    FROM VanBangChungChi vb
    WHERE vb.MaBamSHA256 = @MaBam;
    INSERT INTO LichSuKiemTra (TenFileTaiLen, MaBamFileTaiLen, KetQuaHopLe, GhiChu, IPNguoiKiemTra, ThoiGianKiemTra)
    VALUES (@TenFile, @MaBam, @HopLe, @GhiChu, @IP, SYSDATETIME());
    IF @MaVanBang IS NOT NULL
    BEGIN
        SELECT 
            @HopLe AS KetQuaHopLe,
            @GhiChu AS GhiChu,
            vb.MaVanBang,
            vb.SoHieu,
            vb.TenVanBang,
            vb.NgayCap,
            vb.TrangThai,
            vb.DuongDanFileDaKy,
            vb.ChuKySo,
            vb.MaBamSHA256,
            nn.HoTen AS NguoiNhanHoTen,
            nn.SoCCCD AS NguoiNhanCCCD,
            nn.NgaySinh AS NguoiNhanNgaySinh,
            dv.TenDonVi AS DonViPhatHanhTen,
            dv.MaCode AS DonViPhatHanhCode
        FROM VanBangChungChi vb
        JOIN NguoiNhan nn ON vb.MaNguoiNhan = nn.MaNguoiNhan
        JOIN DonViPhatHanh dv ON vb.MaDonVi = dv.MaDonVi
        WHERE vb.MaVanBang = @MaVanBang;
    END
    ELSE
    BEGIN
        SELECT 
            0 AS KetQuaHopLe,
            @GhiChu AS GhiChu,
            NULL AS MaVanBang,
            NULL AS SoHieu,
            NULL AS TenVanBang,
            NULL AS NgayCap,
            NULL AS TrangThai,
            NULL AS DuongDanFileDaKy,
            NULL AS ChuKySo,
            @MaBam AS MaBamSHA256,
            NULL AS NguoiNhanHoTen,
            NULL AS NguoiNhanCCCD,
            NULL AS NguoiNhanNgaySinh,
            NULL AS DonViPhatHanhTen,
            NULL AS DonViPhatHanhCode;
    END
END;
GO
IF NOT EXISTS (SELECT 1 FROM DonViPhatHanh WHERE MaCode = 'DHBKHN')
BEGIN
    INSERT INTO DonViPhatHanh (TenDonVi, MaCode, Email, TrangThaiHoatDong)
    VALUES 
    (N'Đại học Bách Khoa Hà Nội', 'DHBKHN', 'vanbang@hust.edu.vn', 1),
    (N'Sở Giáo dục và Đào tạo TP. Hà Nội', 'SOGD_HN', 'sogd@hanoi.edu.vn', 1),
    (N'Đại học Quốc gia TP. Hồ Chí Minh', 'DHQG_HCM', 'daotao@vnuhcm.edu.vn', 1);
END
GO
IF NOT EXISTS (SELECT 1 FROM NguoiNhan WHERE SoCCCD = '001201012345')
BEGIN
    INSERT INTO NguoiNhan (HoTen, SoCCCD, Email, NgaySinh)
    VALUES 
    (N'Nguyễn Văn An', '001201012345', 'vanan.nguyen@gmail.com', '2001-05-15'),
    (N'Trần Thị Mai', '079202054321', 'thimai.tran@gmail.com', '2002-11-20'),
    (N'Lê Hoàng Long', '048200088999', 'hoanglong.le@gmail.com', '2000-08-08');
END
GO
IF NOT EXISTS (SELECT 1 FROM KhoaKySo WHERE MaDonVi = 1)
BEGIN
    INSERT INTO KhoaKySo (MaDonVi, PublicKeyText, PrivateKeyMaHoa, NgayHetHan, TrangThai)
    VALUES 
    (1, '<RSAKeyValue><Modulus>w1wY8f...SAMPLE_PUBLIC_KEY...</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>', 
        'ENCRYPTED_PRIVATE_KEY_PAYLOAD_DHBKHN_2026', '2030-12-31', 1),
    (2, '<RSAKeyValue><Modulus>x9zK2b...SAMPLE_PUBLIC_KEY...</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>', 
        'ENCRYPTED_PRIVATE_KEY_PAYLOAD_SOGD_2026', '2028-12-31', 1);
END
GO
IF NOT EXISTS (SELECT 1 FROM VanBangChungChi WHERE SoHieu = 'BK-2026-IT001')
BEGIN
    INSERT INTO VanBangChungChi 
    (MaDonVi, MaNguoiNhan, MaKhoa, TenVanBang, SoHieu, DuongDanFileDaKy, ChuKySo, MaBamSHA256, NgayCap, TrangThai)
    VALUES 
    (1, 1, 1, N'Bằng Kỹ sư Công nghệ Thông tin', 'BK-2026-IT001', 
     '/uploads/certificates/BK_2026_IT001_Signed.pdf', 
     'MEQCIQCc4f...DUMMY_DIGITAL_SIGNATURE_BASE64...iAIfNf3a8b4c2e1',
     'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', 
     '2026-06-30', 'HOP_LE'),
    (1, 2, 1, N'Chứng chỉ Tiếng Anh B2 VSTEP', 'BK-2026-EN002', 
     '/uploads/certificates/BK_2026_EN002_Signed.pdf', 
     'MEQCIDd7a...DUMMY_DIGITAL_SIGNATURE_BASE64...wQIhAOm2b9c8d7e',
     '2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae',
     '2026-07-15', 'HOP_LE');
END
GO
IF NOT EXISTS (SELECT 1 FROM LichSuKiemTra WHERE MaBamFileTaiLen = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855')
BEGIN
    INSERT INTO LichSuKiemTra (TenFileTaiLen, MaBamFileTaiLen, KetQuaHopLe, GhiChu, IPNguoiKiemTra, ThoiGianKiemTra)
    VALUES 
    (N'Bang_Ky_Su_NguyenVanAn.pdf', 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', 1, 
     N'Văn bằng hợp lệ, chữ ký số và mã băm toàn vẹn.', '127.0.0.1', SYSDATETIME()),
    (N'Bang_Ky_Su_BiSuaDoi.pdf', 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', 0, 
     N'Tệp tin không tồn tại trong hệ thống hoặc đã bị chỉnh sửa nội dung.', '192.168.1.15', SYSDATETIME());
END
GO
PRINT N'Cơ sở dữ liệu HeThongVanBangSo và dữ liệu mẫu đã được tạo thành công!';
GO
