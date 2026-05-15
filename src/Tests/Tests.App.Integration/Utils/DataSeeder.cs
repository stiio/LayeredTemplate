using LayeredTemplate.App.Shared.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Tests.App.Integration.Utils;

internal static class DataSeeder
{
    public static void SeedData(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.Migrate();

        dbContext.Users.AddRange(TestUsers.Client, TestUsers.Admin);
        dbContext.SaveChanges();
    }
}
