using System.Text;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;

/// <summary>
/// <see cref="TextWriter"/> that accumulates output in a <see cref="StringBuilder"/> and aborts
/// with <see cref="InvalidOperationException"/> when the cumulative character count exceeds the
/// configured cap. Used by <see cref="LiquidRenderer"/> to bound a single Liquid render — even
/// with <c>MaxSteps</c> set, an attacker can still tune a script to emit large output (each
/// emit is a step or two, so 100k steps × 50-char append = ~5 MB output). The writer cap closes
/// that gap.
/// </summary>
/// <remarks>
/// Async overrides aren't necessary for our use: <see cref="StringBuilder"/> append is purely
/// in-memory, so the base <see cref="TextWriter"/> default of routing async writes back through
/// the sync overrides is correct and free of overhead. The exception thrown from a sync override
/// surfaces correctly through Fluid's awaited write.
/// </remarks>
internal sealed class LimitingStringWriter : TextWriter
{
    private readonly StringBuilder strBuilder = new();
    private readonly int maxChars;

    public LimitingStringWriter(int maxChars)
    {
        if (maxChars <= 0) throw new ArgumentOutOfRangeException(nameof(maxChars));
        this.maxChars = maxChars;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        this.EnsureBudget(1);
        this.strBuilder.Append(value);
    }

    public override void Write(string? value)
    {
        if (value is null) return;

        this.EnsureBudget(value.Length);
        this.strBuilder.Append(value);
    }

    public override void Write(char[]? buffer)
    {
        if (buffer is null) return;

        this.EnsureBudget(buffer.Length);
        this.strBuilder.Append(buffer);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        this.EnsureBudget(count);
        this.strBuilder.Append(buffer, index, count);
    }

    public override void Write(ReadOnlySpan<char> value)
    {
        this.EnsureBudget(value.Length);
        this.strBuilder.Append(value);
    }

    private void EnsureBudget(int incoming)
    {
        if (this.strBuilder.Length + incoming > this.maxChars)
        {
            throw new InvalidOperationException(
                $"Liquid render output exceeded the {this.maxChars}-character limit.");
        }
    }

    public override string ToString() => this.strBuilder.ToString();
}
