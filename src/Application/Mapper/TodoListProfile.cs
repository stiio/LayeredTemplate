using AutoMapper;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Mapper;

public class TodoListProfile : Profile
{
    public TodoListProfile()
    {
        this.CreateMap<TodoList, TodoListDto>();

        this.CreateMap<TodoListUpdateRequestBody, TodoList>();
    }
}