using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Melon.Models;
using Melon.DisplayClasses;
using System.Security.Cryptography.X509Certificates;

namespace Melon.LocalClasses
{
    public class Security
    {
        private static int keySize = 64;
        private static int iterations = 350000;
        private static HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;
        private static SSLConfig sslConfig;
        private static List<string> InviteCodes;
        public IDataProtector _protector;
        public Security(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Melon.SSLConfig.v1");
        }
        public static void SetSSLConfig(string path, string pass)
        {
            sslConfig = new SSLConfig();
            sslConfig.PathToCert = path;
            sslConfig.Password = pass;
        }
        public static void SetSSLConfig(SSLConfig config)
        {
            sslConfig = config;
        }
        public static SSLConfig GetSSLConfig()
        {
            return sslConfig;
        }
        public static string VerifySSLConfig(SSLConfig config)
        {
            try
            {
                var certificate = new X509Certificate2(config.PathToCert, config.Password);

                var expiry = certificate.GetExpirationDateString();

                var expiryDate = DateTime.Parse(expiry);

                if (expiryDate < DateTime.Now)
                {
                    return "Expired";
                }

                return "Valid";
            }
            catch (Exception)
            {
                return "Invalid";
            }
        }
        public static string HashPassword(string password, out byte[] salt)
        {
            salt = RandomNumberGenerator.GetBytes(keySize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                hashAlgorithm,
                keySize);
            return Convert.ToHexString(hash);
        }
        public static string HashPasword(string password, byte[] salt)
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                hashAlgorithm,
                keySize);
            return Convert.ToHexString(hash);
        }

        public static bool VerifyPassword(string password, string hash, byte[] salt)
        {
            var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, hashAlgorithm, keySize);
            return CryptographicOperations.FixedTimeEquals(hashToCompare, Convert.FromHexString(hash));
        }
        public static byte[] GenerateSecretKey()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[32];
                rng.GetBytes(randomBytes);
                return randomBytes;
            }
        }
        public static string GenerateJwtToken(string username, string role, string id, int ExpireInMinutes = 0)
        {
            if(ExpireInMinutes == 0)
            {
                ExpireInMinutes = StateManager.MelonSettings.JWTExpireInMinutes;
            }
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = StateManager.JWTKey;

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new []
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, role),
                    new Claim(ClaimTypes.UserData, id)
                }),
                Expires = DateTime.UtcNow.AddMinutes(ExpireInMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var rToken = tokenHandler.WriteToken(token);
            return rToken;
        }
        public static string CreateInviteCode()
        {
            if(InviteCodes == null)
            {
                InviteCodes = new List<string>();
            }
            if(InviteCodes.Count() >= 25)
            {
                return "Timeout";
            }

            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] stringChars = new char[4];

            for (int i = 0; i < 4; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            string code = new string(stringChars);
            InviteCodes.Add(code);

            Thread timer = new Thread(() =>
            {
                int count = 0;
                while (count < 6000)
                {
                    Thread.Sleep(new TimeSpan(0, 0, 10));
                    count+=10;
                    if (!InviteCodes.Contains(code))
                    {
                        return;
                    }
                }
                InvalidateInviteCode(code);
            });

            return new string(stringChars);
        }
        public static bool ValidateInviteCode(string code)
        {
            if (InviteCodes.Contains(code)){
                return true;
            }
            return false;
        }
        public static void InvalidateInviteCode(string code)
        {
            try
            {
                InviteCodes.Remove(code);
            }
            catch (Exception)
            {

            }
        }
    }
}
