using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto.Parameters;
using Serilog;

namespace Melon.LocalClasses.Networking;

/// <summary>
/// Who this server is to its clients: a stable id, the self-signed HTTPS certificate clients pin by fingerprint, and the
/// Ed25519 key its Pkarr records are published under. All three are made once and kept in Configs.
/// </summary>
public static class NetworkIdentity
{
    private sealed class IdentityFile
    {
        public string? ServerId { get; set; }
        /// <summary>Base64 PFX of the certificate (protected).</summary>
        public string? Certificate { get; set; }
        /// <summary>Base64 Ed25519 seed (protected).</summary>
        public string? PkarrSeed { get; set; }
    }

    private static readonly object Gate = new();
    private static IdentityFile? file;
    private static X509Certificate2? certificate;

    private static IdentityFile Load()
    {
        if (file != null) return file;
        try { file = Storage.LoadConfigFile<IdentityFile>("NetworkIdentity", ["Certificate", "PkarrSeed"], out _); }
        catch (Exception ex) { Log.Warning("Network identity unreadable ({ErrorType}); creating a new one. Devices will need to scan a new QR code.", ex.GetType().Name); }
        file ??= new IdentityFile();
        bool changed = false;
        if (string.IsNullOrEmpty(file.ServerId)) { file.ServerId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(); changed = true; }
        if (string.IsNullOrEmpty(file.PkarrSeed)) { file.PkarrSeed = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); changed = true; }
        if (changed) Save();
        return file;
    }

    private static void Save()
    {
        var copy = new IdentityFile { ServerId = file!.ServerId, Certificate = file.Certificate, PkarrSeed = file.PkarrSeed };
        try { Storage.SaveConfigFile("NetworkIdentity", copy, ["Certificate", "PkarrSeed"]); }
        catch (Exception ex) { Log.Warning("Network identity couldn't be saved ({ErrorType}).", ex.GetType().Name); }
    }

    /// <summary>16 hex characters, the same for the life of this Melon install.</summary>
    public static string ServerId { get { lock (Gate) return Load().ServerId!; } }

    /// <summary>The certificate Automatic networking serves on the HTTPS port. Made on first use; valid for 20 years.</summary>
    public static X509Certificate2 Certificate
    {
        get
        {
            lock (Gate)
            {
                if (certificate != null) return certificate;
                var f = Load();
                if (!string.IsNullOrEmpty(f.Certificate))
                {
                    try { certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(f.Certificate), null, X509KeyStorageFlags.Exportable); }
                    catch (Exception ex) { Log.Warning("Saved HTTPS certificate unreadable ({ErrorType}); making a new one.", ex.GetType().Name); }
                }
                if (certificate == null || !certificate.HasPrivateKey)
                {
                    certificate = Create(f.ServerId!);
                    f.Certificate = Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
                    Save();
                    Log.Information("Made a new HTTPS certificate, fingerprint {Fingerprint}", Fingerprint(certificate));
                }
                return certificate;
            }
        }
    }

    /// <summary>Whether a certificate already exists (so status pages don't make one just by looking).</summary>
    public static bool HasCertificate { get { lock (Gate) return certificate != null || !string.IsNullOrEmpty(Load().Certificate); } }

    private static X509Certificate2 Create(string serverId)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN=Melon {serverId}, O=Melon", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        var host = Environment.MachineName.ToLowerInvariant();
        if (Uri.CheckHostName(host) == UriHostNameType.Dns) { names.AddDnsName(host); names.AddDnsName(host + ".local"); }
        names.AddIpAddress(System.Net.IPAddress.Loopback);
        names.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
        // Round-trip through PFX so the private key is usable by SslStream on Windows (ephemeral keys aren't).
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>SHA-256 of the certificate's DER bytes, base64url without padding (43 characters).</summary>
    public static string Fingerprint(X509Certificate2 certificate) => Base64Url(SHA256.HashData(certificate.RawData));

    public static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The Ed25519 key Pkarr records are signed with.</summary>
    public static Ed25519PrivateKeyParameters PkarrKey
    {
        get { lock (Gate) return new Ed25519PrivateKeyParameters(Convert.FromBase64String(Load().PkarrSeed!)); }
    }

    /// <summary>The Pkarr public key, z-base32 (52 characters). This is what clients look up.</summary>
    public static string PkarrPublicKey => Pkarr.ZBase32(PkarrKey.GeneratePublicKey().GetEncoded());
}
