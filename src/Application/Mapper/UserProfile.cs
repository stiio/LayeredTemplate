using AutoMapper;
using LayeredTemplate.Application.Contracts.Models.Users;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Mapper;

internal class UserProfile : Profile
{
    public UserProfile()
    {
        this.CreateMap<User, CurrentUser>();

        this.CreateMap<User, UserShortInfo>();
    }
}