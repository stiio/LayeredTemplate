using System.Security.Cryptography.X509Certificates;
using LayeredTemplate.App.Setup.StartupTasks;
using LayeredTemplate.App.Shared.Db;
using LayeredTemplate.App.Shared.Options;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.DataProtection;

namespace LayeredTemplate.App.Setup;

public static class ConfigureDataProtection
{
    public static IServiceCollection AddAppDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DataProtectionSettings>(configuration.GetSection(nameof(DataProtectionSettings)));

        // Disable runtime auto-generation — keys are created by RotateDataProtectionKeysTask at startup
        // under a distributed lock. This prevents race conditions when multiple instances start simultaneously.
        var dataProtection = services.AddDataProtection()
            .DisableAutomaticKeyGeneration()
            .SetApplicationName("LayeredTemplate.App")
            .PersistKeysToDbContext<AppDbContext>();

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
                var unprotectCertificate = X509CertificateLoader.LoadPkcs12(unprotectCertBytes, unprotectCertificateItemData.Password);
                dataProtection.UnprotectKeysWithAnyCertificate(unprotectCertificate);
            }
        }

        services.AddStartupTask<RotateDataProtectionKeysTask>();

        return services;
    }
}