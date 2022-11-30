using LayeredTemplate.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Web.Api.IntegrationTests.Utils;

internal static class DataSeeder
{
    public static void SeedData(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Database.Migrate();

        dbContext.Users.AddRange(TestUsers.Client, TestUsers.Admin);
        dbContext.SaveChanges();
    }
}