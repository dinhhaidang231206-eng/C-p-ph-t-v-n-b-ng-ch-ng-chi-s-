namespace HeThongVanBangSo.Services
{
    public interface ISignatureService
    {
        /// <summary>
        /// Tính toán mã băm SHA-256 (64 ký tự hex) từ Stream dữ liệu tệp
        /// </summary>
        Task<string> ComputeSha256HashAsync(Stream stream);

        /// <summary>
        /// Tính toán mã băm SHA-256 từ mảng byte
        /// </summary>
        string ComputeSha256Hash(byte[] data);

        /// <summary>
        /// Sinh cặp khóa RSA 2048-bit (Khóa công khai & Khóa bí mật dạng XML/PEM)
        /// </summary>
        (string PublicKey, string PrivateKey) GenerateRsaKeyPair(int keySize = 2048);

        /// <summary>
        /// Ký số dữ liệu bằng khóa bí mật (Private Key) sử dụng thuật toán RSA-SHA256
        /// </summary>
        string SignData(byte[] data, string privateKeyXml);

        /// <summary>
        /// Xác thực chữ ký số bằng khóa công khai (Public Key)
        /// </summary>
        bool VerifySignature(byte[] data, string signatureBase64, string publicKeyXml);
    }
}
