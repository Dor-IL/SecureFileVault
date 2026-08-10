using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace fileVault
{
    // The server and every client run on the same machine (VaultClient always connects to
    // 127.0.0.1), so instead of a real CA we generate one self-signed certificate the first
    // time any instance needs it and cache it under LocalApplicationData. Every subsequent
    // server or client reads that same file, which lets the client pin the exact certificate
    // (by thumbprint) rather than trusting any self-signed cert a peer happens to present.
    static class TlsCertificateProvider
    {
        private static readonly string CertPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "fileVault", "server.pfx");

        private const string CertPassword = "fileVault-local-tls";
        private const string SubjectName = "CN=fileVault-localhost";

        private static X509Certificate2 _cached;
        private static readonly object Lock = new object();

        public static X509Certificate2 GetOrCreateServerCertificate()
        {
            lock (Lock)
            {
                if (_cached != null)
                    return _cached;

                if (File.Exists(CertPath))
                {
                    try
                    {
                        _cached = X509CertificateLoader.LoadPkcs12FromFile(CertPath, CertPassword, X509KeyStorageFlags.Exportable);
                        return _cached;
                    }
                    catch
                    {
                        // Corrupt or unreadable; fall through and regenerate.
                    }
                }

                _cached = CreateAndPersist();
                return _cached;
            }
        }

        private static X509Certificate2 CreateAndPersist()
        {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest(SubjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            using X509Certificate2 generated = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

            byte[] pfxBytes = generated.Export(X509ContentType.Pfx, CertPassword);

            Directory.CreateDirectory(Path.GetDirectoryName(CertPath)!);
            File.WriteAllBytes(CertPath, pfxBytes);

            return X509CertificateLoader.LoadPkcs12(pfxBytes, CertPassword, X509KeyStorageFlags.Exportable);
        }
    }
}
