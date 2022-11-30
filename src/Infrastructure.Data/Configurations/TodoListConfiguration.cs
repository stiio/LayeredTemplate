using LayeredTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Infrastructure.Data.Configurations;

internal class TodoListConfiguration : IEntityTypeConfiguration<TodoList>
{
    public void Configure(EntityTypeBuilder<TodoList> builder)
    {
        builder.Property(todoList => todoList.Name)
            .HasMaxLength(255);

        builder.Property(todoList => todoList.Type)
            .HasConversion<string>()
            .HasMaxLength(8);
    }
}