using System.Security.Cryptography;
using System.Text;

namespace HeThongVanBangSo.Services
{
    public class SignatureService : ISignatureService
    {
        private readonly ILogger<SignatureService> _logger;

        public SignatureService(ILogger<SignatureService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Băm luồng file thành chuỗi Hex 64 ký tự SHA-256
        /// </summary>
        public async Task<string> ComputeSha256HashAsync(Stream stream)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Băm mảng byte thành chuỗi Hex 64 ký tự SHA-256
        /// </summary>
        public string ComputeSha256Hash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Sinh cặp khóa RSA 2048-bit (Public Key và Private Key)
        /// </summary>
        public (string PublicKey, string PrivateKey) GenerateRsaKeyPair(int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);
            
            // Xuất khóa công khai ở định dạng PEM
            string publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

            // Xuất khóa bí mật ở định dạng PEM (PKCS#8)
            string privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

            return (publicKeyPem, privateKeyPem);
        }

        /// <summary>
        /// Ký số dữ liệu file bằng Private Key RSA-SHA256
        /// </summary>
        public string SignData(byte[] data, string privateKeyPem)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(privateKeyPem);

                byte[] signatureBytes = rsa.SignData(
                    data,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );

                return Convert.ToBase64String(signatureBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình ký số bằng Private Key");
                throw new InvalidOperationException("Không thể ký số dữ liệu: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Xác thực chữ ký số bằng Public Key RSA-SHA256
        /// </summary>
        public bool VerifySignature(byte[] data, string signatureBase64, string publicKeyPem)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(publicKeyPem);

                byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

                return rsa.VerifyData(
                    data,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi xác thực chữ ký số");
                return false;
            }
        }
    }
}
