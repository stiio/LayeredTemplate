using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

/// <summary>
/// Creates the Admin role if missing and assigns it to the user referenced by
/// <see cref="InitialAdminUserSettings.Email"/>, if that user already exists.
/// Does not create the user — bootstrapping the first admin requires the user
/// to register through the normal flow first.
/// </summary>
public class SeedAdminRoleTask : IStartupTask
{
    private readonly RoleManager<ApplicationRole> roleManager;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly InitialAdminUserSettings settings;
    private readonly ILockProvider lockProvider;
    private readonly ILogger<SeedAdminRoleTask> logger;

    public SeedAdminRoleTask(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<InitialAdminUserSettings> settings,
        ILockProvider lockProvider,
        ILogger<SeedAdminRoleTask> logger)
    {
        this.roleManager = roleManager;
        this.userManager = userManager;
        this.settings = settings.Value;
        this.lockProvider = lockProvider;
        this.logger = logger;
    }

    public int Order => 15;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var @lock = await this.lockProvider.AcquireLockAsync(
            "seed-admin-role",
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        if (!await this.roleManager.RoleExistsAsync(AppRoles.Admin))
        {
            var result = await this.roleManager.CreateAsync(new ApplicationRole { Name = AppRoles.Admin });
            if (!result.Succeeded)
            {
                this.logger.LogError(
                    "Failed to create {Role} role: {Errors}",
                    AppRoles.Admin,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }

            this.logger.LogInformation("Created {Role} role.", AppRoles.Admin);
        }

        if (string.IsNullOrWhiteSpace(this.settings.Email))
        {
            return;
        }

        var user = await this.userManager.FindByEmailAsync(this.settings.Email);
        if (user is null)
        {
            var createUserResult = await this.userManager.CreateAsync(new ApplicationUser
            {
                UserName = this.settings.Email,
                Email = this.settings.Email,
                EmailConfirmed = true,
            });

            if (!createUserResult.Succeeded)
            {
                this.logger.LogError("Failed create admin user: {@Errors}.", createUserResult.Errors);
                return;
            }

            user = await this.userManager.FindByEmailAsync(this.settings.Email);
        }

        if (user is null)
        {
            this.logger.LogError("Failed create admin user.");
            return;
        }

        if (!await this.userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var result = await this.userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (result.Succeeded)
            {
                this.logger.LogInformation("Assigned {Role} role to {Email}.", AppRoles.Admin, this.settings.Email);
            }
            else
            {
                this.logger.LogError(
                    "Failed to assign {Role} role to {Email}: {Errors}",
                    AppRoles.Admin,
                    this.settings.Email,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
