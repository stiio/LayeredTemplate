using System.Collections;
using LayeredTemplate.App.Domain.Entities;

namespace LayeredTemplate.Tests.App.Integration.Utils;

public class TestUsers : IEnumerable<object[]>
{
    public static User Client { get; } = new User()
    {
        Id = new Guid("361f7c7a-0c25-4e6d-a3d9-d2584a9d80e6"),
        Email = "sample_client@yopmail.com",
    };

    public static User Admin { get; } = new User()
    {
        Id = new Guid("D43B70E4-0EEB-4388-8566-C9485D3C9D48"),
        Email = "sample_admin@yopmail.com",
    };

    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { Client };
        yield return new object[] { Admin };
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}