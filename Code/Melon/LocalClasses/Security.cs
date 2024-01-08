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
using Amazon.Util;

namespace Melon.LocalClasses
{
    public static class Security
    {
        private static int keySize = 64;
        private static int iterations = 350000;
        private static HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;
        private static SSLConfig sslConfig;
        public static void LoadSSLConfig()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var services = serviceCollection.BuildServiceProvider();

            string txt = File.ReadAllText($"{StateManager.melonPath}/SSLConfig.json");
            sslConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<SSLConfig>(txt);

            var instance = ActivatorUtilities.CreateInstance<SSLConfig>(services);
            sslConfig.PathToCert = sslConfig.PathToCert;
            sslConfig.Password = instance.Decrypt(sslConfig.Password);
        }
        public static void SaveSSLConfig()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var services = serviceCollection.BuildServiceProvider();

            var instance = ActivatorUtilities.CreateInstance<SSLConfig>(services);
            sslConfig.Password = instance.Encrypt(sslConfig.Password);

            string txt = Newtonsoft.Json.JsonConvert.SerializeObject(sslConfig);
            File.WriteAllText($"{StateManager.melonPath}/SSLConfig.json", txt);
        }
        public static void SetSSLConfig(string path, string pass)
        {
            sslConfig = new SSLConfig();
            sslConfig.PathToCert = path;
            sslConfig.Password = pass;
        }
        public static KeyValuePair<string, string> GetSSLConfig()
        {
            return new KeyValuePair<string, string>(sslConfig.PathToCert, sslConfig.Password);
        }
        public static string HashPasword(string password, out byte[] salt)
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
        public static string GenerateSecretKey()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[32];
                rng.GetBytes(randomBytes);
                return Convert.ToBase64String(randomBytes);
            }
        }
        public static string GenerateJwtToken(string username, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(StateManager.MelonSettings.JWTKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new []
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, role)
                }),
                Expires = DateTime.UtcNow.AddMinutes(StateManager.MelonSettings.JWTExpireInMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

    }
}
