using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// <see cref="CorrelationKeyLog"/> — the PHI-hardening helper that renders an author-controlled
/// correlation key for logs as a stable, non-reversible short hash (so ops can still correlate a
/// suspend log to its signal log without the raw, possibly-PHI key reaching the sink).
/// </summary>
public class CorrelationKeyLogTests
{
    [Fact]
    public void Same_key_always_hashes_to_the_same_token()
    {
        Assert.Equal(CorrelationKeyLog.Hash("patient@example.com"), CorrelationKeyLog.Hash("patient@example.com"));
    }

    [Fact]
    public void Different_keys_hash_to_different_tokens()
    {
        Assert.NotEqual(CorrelationKeyLog.Hash("a"), CorrelationKeyLog.Hash("b"));
    }

    [Fact]
    public void Token_does_not_contain_the_raw_key_value()
    {
        const string key = "form-submit:patient@example.com";
        var token = CorrelationKeyLog.Hash(key);
        Assert.DoesNotContain("patient", token);
        Assert.DoesNotContain("@example.com", token);
        Assert.StartsWith("sha256:", token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_or_null_renders_a_fixed_sentinel_not_a_hash_of_empty(string? key)
    {
        Assert.Equal("sha256:<empty>", CorrelationKeyLog.Hash(key));
    }
}
