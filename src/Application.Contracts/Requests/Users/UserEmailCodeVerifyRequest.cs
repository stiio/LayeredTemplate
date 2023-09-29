﻿using System.ComponentModel.DataAnnotations;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.Users;

public class UserEmailCodeVerifyRequest : IRequest
{
    /// <summary>
    /// Email
    /// </summary>
    /// <example>example@email.com</example>
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Code
    /// </summary>
    /// <example>124567</example>
    [Required]
    public int Code { get; set; }
}