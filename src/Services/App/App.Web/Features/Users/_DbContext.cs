using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.App.Shared.Db;

/// <summary>
/// Users feature's slice of <see cref="AppDbContext"/>. Partial class declaration so DbSets
/// live next to the feature they belong to, not in a shared "god" file.
/// </summary>
public partial class AppDbContext
{
    public DbSet<App.Features.Users.User> Users { get; set; } = null!;
}
