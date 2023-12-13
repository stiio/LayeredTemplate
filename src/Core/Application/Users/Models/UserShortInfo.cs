namespace LayeredTemplate.Application.Users.Models;

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