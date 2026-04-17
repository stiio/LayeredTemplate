using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;

public class ApplicationRole : IdentityRole
{
    public override string Id { get; set; } = Guid.CreateVersion7().ToString();
}