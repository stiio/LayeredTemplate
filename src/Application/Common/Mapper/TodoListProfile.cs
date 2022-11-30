using AutoMapper;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Common.Mapper;

public class TodoListProfile : Profile
{
    public TodoListProfile()
    {
        this.CreateMap<TodoList, TodoListDto>();

        this.CreateMap<TodoListUpdateRequest, TodoList>();
    }
}