using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.ApiKeys.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.ApiKeys.Requests;

public class ApiKeyCreateRequest : IRequest<ApiKeySecretDto>
{
    [Required]
    [FromBody]
    public ApiKeyCreateRequestBody Body { get; set; } = null!;
}