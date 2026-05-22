namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>Raised when an Expr resolver fails to evaluate or coerce a value.</summary>
public class ExpressionResolutionException : Exception
{
    public string Engine { get; }

    public string Path { get; }

    public string TargetType { get; }

    public ExpressionResolutionException(string engine, string path, string targetType, string reason, Exception? inner = null)
        : base($"Expression failed at '{path}' (engine={engine}, target={targetType}): {reason}", inner)
    {
        this.Engine = engine;
        this.Path = path;
        this.TargetType = targetType;
    }
}
