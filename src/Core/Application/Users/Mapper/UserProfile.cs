using AutoMapper;
using LayeredTemplate.Application.Users.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Users.Mapper;

internal class UserProfile : Profile
{
    public UserProfile()
    {
        this.CreateMap<User, CurrentUser>();

        this.CreateMap<User, UserShortInfo>();
    }
}