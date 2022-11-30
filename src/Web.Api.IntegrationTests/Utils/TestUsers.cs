using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Web.Api.IntegrationTests.Utils;

public static class TestUsers
{
    public static User Client { get; } = new User()
    {
        Id = new Guid("361f7c7a-0c25-4e6d-a3d9-d2584a9d80e6"),
        Email = "sample_client@yopmail.com",
        Role = Role.Client,
    };

    public static User Admin { get; } = new User()
    {
        Id = new Guid("D43B70E4-0EEB-4388-8566-C9485D3C9D48"),
        Email = "sample_admin@yopmail.com",
        Role = Role.Admin,
    };

    public static User NotSeedClient { get; } = new User()
    {
        Id = new Guid("A30F16DF-F714-4764-81FD-5A5DF565FDF1"),
        Email = "sample_client_not_seed@yopmail.com",
        Role = Role.Client,
    };
}