using System.Security.Cryptography.X509Certificates;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using Microsoft.AspNetCore.DataProtection;

namespace LayeredTemplate.Auth.Web.Infrastructure.DataProtection;

public static class ServicesExtensions
{
    public static void AddAppDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("LayeredTemplate.Auth")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(180))
            .PersistKeysToDbContext<AuthDbContext>();

        var dataProtectionSettings = configuration.GetSection(nameof(DataProtectionSettings)).Get<DataProtectionSettings>()!;
        if (!string.IsNullOrEmpty(dataProtectionSettings.CertificateBase64))
        {
            var certBytes = Convert.FromBase64String(dataProtectionSettings.CertificateBase64);
            var certificate = X509CertificateLoader.LoadPkcs12(certBytes, dataProtectionSettings.CertificatePassword);
            dataProtection.ProtectKeysWithCertificate(certificate);

            foreach (var unprotectCertificateItemData in dataProtectionSettings.UnprotectCertificates
                         .Where(item => !string.IsNullOrEmpty(item.Base64)))
            {
                var unprotectCertBytes = Convert.FromBase64String(unprotectCertificateItemData.Base64);
                var unprotectCertificate = X509CertificateLoader.LoadPkcs12(certBytes, unprotectCertificateItemData.Password);
                dataProtection.UnprotectKeysWithAnyCertificate(unprotectCertificate);
            }
        }
    }
}