namespace LayeredTemplate.Application.Contracts.Models.Users;

public class UserShortInfo
{
    /// <summary>
    /// Id of user
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Email of user
    /// </summary>
    public string? Email { get; set; }
}