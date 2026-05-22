namespace LayeredTemplate.App.Shared.Options;

/// openssl req -x509 -newkey rsa:2048  -keyout dp-key.pem -out dp-cert.pem -days 1825 -nodes -subj "/CN=DataProtection"
/// openssl pkcs12 -export -out dp-cert.pfx -inkey dp-key.pem -in dp-cert.pem -passout pass:your-password
public class DataProtectionSettings
{
    public string CertificateBase64 { get; set; } = string.Empty;

    public string CertificatePassword { get; set; } = string.Empty;

    public DataProtectionUnprotectCertificate[] UnprotectCertificates { get; set; } = [];

    public class DataProtectionUnprotectCertificate
    {
        public string Base64 { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}