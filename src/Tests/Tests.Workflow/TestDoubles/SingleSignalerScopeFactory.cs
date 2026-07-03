using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Minimal scope factory whose scope resolves the one captured <see cref="IWorkflowSignaler"/> —
/// models the fresh-scope path <c>SendSignalActionType</c> uses for re-entrancy isolation
/// without spinning up a real container.
/// </summary>
internal sealed class SingleSignalerScopeFactory : IServiceScopeFactory, IServiceProvider, IServiceScope
{
    private readonly IWorkflowSignaler signaler;

    public SingleSignalerScopeFactory(IWorkflowSignaler signaler) => this.signaler = signaler;

    public IServiceScope CreateScope() => this;

    public IServiceProvider ServiceProvider => this;

    public object? GetService(Type serviceType)
        => serviceType == typeof(IWorkflowSignaler) ? this.signaler : null;

    public void Dispose()
    {
    }
}
