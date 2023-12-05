using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.ApiKeys;

public class ApiKeyUpdateRequest : IRequest<ApiKeyDto>
{
    public Guid Id { get; set; }

    [Required]
    [FromBody]
    public ApiKeyUpdateRequestBody Body { get; set; } = null!;
}