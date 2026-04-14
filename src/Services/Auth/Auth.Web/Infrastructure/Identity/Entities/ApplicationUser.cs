using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public override string Id { get; set; } = Guid.CreateVersion7().ToString();
}