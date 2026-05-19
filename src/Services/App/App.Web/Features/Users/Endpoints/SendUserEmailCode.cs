using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Shared.Endpoints;
using LayeredTemplate.App.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.Users;

[EndpointGroup<UsersGroup>]
public sealed class SendUserEmailCode : IEndpoint
{
    public sealed class Request
    {
        /// <summary>Email</summary>
        /// <example>example@email.com</example>
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/email/send_code", Handle)
            .WithName(nameof(SendUserEmailCode))
            .WithSummary("Send user email verification code")
            .WithValidation<Request>();

    public static Task<NoContent> Handle([FromBody] Request request) =>
        Task.FromResult(TypedResults.NoContent());
}
