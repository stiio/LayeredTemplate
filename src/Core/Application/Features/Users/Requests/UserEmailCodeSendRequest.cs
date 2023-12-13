using System.ComponentModel.DataAnnotations;
using MediatR;

namespace LayeredTemplate.Application.Features.Users.Requests;

public class UserEmailCodeSendRequest : IRequest
{
    /// <summary>
    /// Email
    /// </summary>
    /// <example>example@email.com</example>
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;
}