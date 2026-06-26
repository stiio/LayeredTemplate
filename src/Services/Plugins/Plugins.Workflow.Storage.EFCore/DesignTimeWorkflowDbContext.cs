using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

internal class DesignTimeWorkflowDbContext : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Port=5432;Database=test;Username=test;Password=test;",
                x =>
                {
                    x.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.FullName);
                    x.MigrationsHistoryTable("__EFMigrationsHistory", WorkflowDbContext.SchemaName);
                });

        return new WorkflowDbContext(optionsBuilder.Options);
    }
}