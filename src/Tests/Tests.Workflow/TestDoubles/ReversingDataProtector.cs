using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Toy "encryption": reverses the byte array. Round-trippable, deterministic, no real security —
/// sufficient to verify the protected-column converters' wrapping/unwrapping logic.
/// </summary>
internal sealed class ReversingDataProtector : IWorkflowDataProtector
{
    public byte[] Protect(byte[] plaintext)
    {
        var reversed = (byte[])plaintext.Clone();
        Array.Reverse(reversed);
        return reversed;
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        var reversed = (byte[])ciphertext.Clone();
        Array.Reverse(reversed);
        return reversed;
    }
}
