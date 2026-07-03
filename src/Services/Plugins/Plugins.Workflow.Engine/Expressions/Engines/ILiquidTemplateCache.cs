using Fluid;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;

/// <summary>
/// Singleton cache of compiled Fluid templates keyed by source string. Split out from
/// <see cref="LiquidExpressionEngine"/> so the engine itself can be scoped (it depends on
/// scoped <see cref="Extensions.ILiquidExtension"/> instances) without throwing away
/// compilation work between requests.
/// </summary>
internal interface ILiquidTemplateCache
{
    IFluidTemplate GetOrCompile(string template);
}
