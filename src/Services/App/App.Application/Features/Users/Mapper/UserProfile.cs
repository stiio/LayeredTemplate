using AutoMapper;
using LayeredTemplate.App.Application.Features.Users.Models;
using LayeredTemplate.App.Domain.Entities;

namespace LayeredTemplate.App.Application.Features.Users.Mapper;

internal class UserProfile : Profile
{
    public UserProfile()
    {
        this.CreateMap<User, CurrentUser>();

        this.CreateMap<User, UserShortInfo>();
    }
}