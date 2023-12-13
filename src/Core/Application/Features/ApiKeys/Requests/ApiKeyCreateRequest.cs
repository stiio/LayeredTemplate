using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Features.ApiKeys.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Features.ApiKeys.Requests;

public class ApiKeyCreateRequest : IRequest<ApiKeySecretDto>
{
    [Required]
    [FromBody]
    public ApiKeyCreateRequestBody Body { get; set; } = null!;
}