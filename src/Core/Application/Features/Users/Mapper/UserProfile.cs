using AutoMapper;
using LayeredTemplate.Application.Features.Users.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Features.Users.Mapper;

internal class UserProfile : Profile
{
    public UserProfile()
    {
        this.CreateMap<User, CurrentUser>();

        this.CreateMap<User, UserShortInfo>();
    }
}