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
using MongoDB.Driver;
using System.Security.Cryptography.X509Certificates;

namespace Melon.LocalClasses
{
    public class Security
    {
        private static int keySize = 64;
        private static int iterations = 350000;
        private static HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;
        private static SSLConfig sslConfig;
        private static readonly object CodeLock = new();
        private static readonly Dictionary<string, DateTime> InviteCodes = new(StringComparer.Ordinal);
        private sealed record ResetGrant(string UserId, string PasswordHash, DateTime ExpiresUtc);
        private static readonly Dictionary<string, ResetGrant> ResetCodes = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, DateTime> SignupTickets = new(StringComparer.Ordinal);
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
        public static string SessionStamp(string passwordHash) =>
            Convert.ToHexString(HMACSHA256.HashData(StateManager.JWTKey, Encoding.UTF8.GetBytes(passwordHash)));

        public static string GenerateJwtToken(string username, string role, string id, int ExpireInMinutes = 0)
        {
            var hash = role == "Server" && id == "NA" ? null : StateManager.DbClient.GetDatabase("Melon")
                .GetCollection<User>("Users").Find(u => u._id == id).Project(u => u.Password).FirstOrDefault();
            return GenerateJwtToken(username, role, id, ExpireInMinutes, hash);
        }

        public static string GenerateJwtToken(string username, string role, string id, int ExpireInMinutes, string? passwordHash)
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
                    new Claim(ClaimTypes.UserData, id),
                    new Claim("melon_session", passwordHash == null ? "" : SessionStamp(passwordHash))
                }),
                Expires = DateTime.UtcNow.AddMinutes(ExpireInMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            if (role == "Server" && id == "NA")
            {
                var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                tokenDescriptor.Subject.AddClaim(new Claim("melon_signup", ticket));
                lock (CodeLock) { RemoveExpired(); SignupTickets.Add(ticket, tokenDescriptor.Expires!.Value); }
            }
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var rToken = tokenHandler.WriteToken(token);
            return rToken;
        }
        internal static void ClearAccessCodes()
        {
            lock (CodeLock) { InviteCodes.Clear(); ResetCodes.Clear(); SignupTickets.Clear(); }
        }

        private static void RemoveExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var key in SignupTickets.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) SignupTickets.Remove(key);
            foreach (var key in InviteCodes.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) InviteCodes.Remove(key);
            foreach (var key in ResetCodes.Where(x => x.Value.ExpiresUtc <= now).Select(x => x.Key).ToArray()) ResetCodes.Remove(key);
        }

        internal static Melon.Interface.AccessCode IssueInvite(TimeSpan lifetime)
        {
            if (lifetime <= TimeSpan.Zero || lifetime > DateTime.MaxValue - DateTime.UtcNow)
                throw new ArgumentException("Choose a positive invite lifetime.");
            lock (CodeLock)
            {
                RemoveExpired();
                if (InviteCodes.Count >= 25) throw new InvalidOperationException("25 invites are already active.");
                var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
                var expiry = DateTime.UtcNow.Add(lifetime);
                InviteCodes.Add(code, expiry);
                return new(code, expiry);
            }
        }
        public static string CreateInviteCode(TimeSpan expireTime)
        {
            try { return IssueInvite(expireTime).Code; }
            catch (InvalidOperationException) { return "Timeout"; }
        }
        public static Dictionary<string, DateTime> ViewInviteCodes()
        {
            lock (CodeLock) { RemoveExpired(); return new(InviteCodes); }
        }
        public static bool ValidateInviteCode(string code)
        {
            lock (CodeLock) return InviteCodes.TryGetValue(code, out var end) && end > DateTime.UtcNow;
        }
        public static bool RegisterInvitedUser(string? ticket, Action create)
        {
            lock (CodeLock)
            {
                if (ticket == null || !SignupTickets.TryGetValue(ticket, out var expires) || expires <= DateTime.UtcNow) return false;
                create();
                SignupTickets.Remove(ticket);
                return true;
            }
        }

        public static bool RedeemInvite(string code)
        {
            lock (CodeLock) return InviteCodes.Remove(code, out var end) && end > DateTime.UtcNow;
        }
        public static void InvalidateInviteCode(string code)
        {
            lock (CodeLock) InviteCodes.Remove(code);
        }

        private static string Digest(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        internal static Melon.Interface.AccessCode IssueReset(string id, string passwordHash)
        {
            lock (CodeLock)
            {
                RemoveExpired();
                foreach (var key in ResetCodes.Where(x => x.Value.UserId == id).Select(x => x.Key).ToArray()) ResetCodes.Remove(key);
                var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                var end = DateTime.UtcNow.AddMinutes(10);
                ResetCodes.Add(Digest(code), new(id, passwordHash, end));
                return new(code, end);
            }
        }
        internal static bool RedeemReset(string code, Func<string, string, bool> update)
        {
            if (code == null || code.Length != 32) return false;
            var key = Digest(code.ToUpperInvariant());
            lock (CodeLock)
            {
                if (!ResetCodes.TryGetValue(key, out var grant) || grant.ExpiresUtc <= DateTime.UtcNow) return false;
                if (!update(grant.UserId, grant.PasswordHash)) return false;
                ResetCodes.Remove(key);
                return true;
            }
        }
    }
}
