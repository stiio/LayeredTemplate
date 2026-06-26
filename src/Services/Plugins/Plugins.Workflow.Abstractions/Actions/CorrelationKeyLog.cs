using System.Security.Cryptography;
using System.Text;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// Renders an opaque correlation key for logging WITHOUT exposing its raw value. WaitForm keys are
/// GUID-only (PHI-free), but the generic <c>WaitSignal</c> / <c>SendSignal</c> engine actions accept
/// an author-controlled key that could carry PHI (an email, a name, …). Logging the raw key would
/// leak it; logging nothing makes wait↔signal correlation in ops impossible. The compromise: a
/// stable, non-reversible short hash — same key always renders the same token, so an operator can
/// still match a suspend log to its signal log, but the value itself never reaches the log sink.
/// <para>
/// Lives in Abstractions (public) so App-layer domain adapters that hash their own correlation keys
/// (a future WaitWebhook, the App's WaitForm logging, …) share this exact helper instead of
/// re-implementing the format.
/// </para>
/// </summary>
public static class CorrelationKeyLog
{
    /// <summary>Hex characters of the SHA-256 digest to emit — enough to correlate, far too short to brute-force a meaningful preimage of a structured key.</summary>
    private const int HashHexLength = 16;

    /// <summary>
    /// Non-reversible short token for <paramref name="correlationKey"/>: <c>"sha256:" + first
    /// <see cref="HashHexLength"/> hex chars of its SHA-256</c>. Null / empty render as a fixed
    /// sentinel rather than hashing the empty string, so an absent key is obvious in logs.
    /// </summary>
    public static string Hash(string? correlationKey)
    {
        if (string.IsNullOrEmpty(correlationKey)) return "sha256:<empty>";

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(correlationKey));
        var hex = Convert.ToHexStringLower(digest);
        return $"sha256:{hex[..HashHexLength]}";
    }
}
