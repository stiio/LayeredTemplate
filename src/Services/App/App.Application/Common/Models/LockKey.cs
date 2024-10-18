namespace LayeredTemplate.App.Application.Common.Models;

public class LockKey
{
    private LockKey(string name)
    {
        this.Name = name;
    }

    public string Name { get; set; }

    public static LockKey Migrations(string dbContextName) => new LockKey($"migrations:{dbContextName}");
}