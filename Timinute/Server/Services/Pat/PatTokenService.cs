using System.Security.Cryptography;
using System.Text;

namespace Timinute.Server.Services.Pat
{
    // Tokens are 256 bits of CSPRNG output, so a single SHA-256 (fast) is the correct
    // at-rest transform — a password hasher (bcrypt/PBKDF2) would only add cost with no
    // security benefit for high-entropy secrets.
    public sealed class PatTokenService : IPatTokenService
    {
        public (string plaintext, string hash, string prefix) Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var body = Base64UrlEncode(bytes);
            var plaintext = IPatTokenService.TokenPrefix + body;
            var prefix = body.Substring(0, 8);
            return (plaintext, Hash(plaintext), prefix);
        }

        public string Hash(string plaintext)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
            return Convert.ToHexStringLower(digest);
        }

        public bool FixedTimeEquals(string hashA, string hashB)
            => CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hashA), Encoding.UTF8.GetBytes(hashB));

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
