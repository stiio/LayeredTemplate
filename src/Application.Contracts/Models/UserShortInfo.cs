namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// User Short Info
/// </summary>
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