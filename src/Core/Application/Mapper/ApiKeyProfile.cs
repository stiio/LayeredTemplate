using AutoMapper;
using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Mapper;

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