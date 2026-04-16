namespace LayeredTemplate.Auth.Web.Infrastructure.Options.Models;

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