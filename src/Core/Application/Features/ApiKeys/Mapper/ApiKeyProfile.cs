using AutoMapper;
using LayeredTemplate.Application.Features.ApiKeys.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Features.ApiKeys.Mapper;

internal class ApiKeyProfile : Profile
{
    public ApiKeyProfile()
    {
        this.CreateMap<ApiKey, ApiKeyDto>();
        this.CreateMap<ApiKey, ApiKeySecretDto>();

        this.CreateMap<ApiKeyCreateRequestBody, ApiKey>();
        this.CreateMap<ApiKeyUpdateRequestBody, ApiKey>();
    }
}