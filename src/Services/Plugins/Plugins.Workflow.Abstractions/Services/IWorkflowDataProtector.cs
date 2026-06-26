namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Optional consumer-supplied symmetric encryption hook for workflow PHI columns. Register via
/// <c>builder.AddWorkflowDataProtector&lt;TImpl&gt;()</c> at startup; without registration, the
/// engine writes plaintext bytes (UTF-8) into the same <c>bytea</c> columns and reads them back
/// transparently. Schema is unified — column type doesn't change with the toggle, only the
/// payload format does. That lets consumers turn protection on later without migrating data,
/// and lets the engine read mixed-mode rows during the transition.
/// </summary>
/// <remarks>
/// <para>
/// <b>Storage format (engine-internal, not consumer-visible):</b>
/// </para>
/// <list type="bullet">
///   <item><c>plaintext bytes</c> when no protector is registered (or row pre-dates protection).</item>
///   <item><c>0x80 || ciphertext</c> when a protector wrote it. <c>0x80</c> is a UTF-8
///   continuation byte and can never appear as the first byte of valid UTF-8 text — that's
///   how the engine distinguishes encrypted from plaintext on read.</item>
/// </list>
/// <para>
/// <b>Key rotation:</b> implementations are expected to manage a key ring internally. <see cref="Unprotect"/>
/// must succeed for any ciphertext written with any historical key the implementation still
/// retains. <see cref="Protect"/> always uses the active key. Key identification lives inside
/// each ciphertext blob (the wire format embeds the key id), so a re-encryption sweep can
/// inspect individual values rather than relying on a per-row stamp.
/// </para>
/// <para>
/// <b>Threading:</b> implementations must be thread-safe — <see cref="Protect"/> and
/// <see cref="Unprotect"/> are called concurrently from multiple worker loops.
/// </para>
/// </remarks>
public interface IWorkflowDataProtector
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the active key. Engine wraps the result with
    /// a magic-byte prefix before storage; the implementation should return raw ciphertext only.
    /// </summary>
    byte[] Protect(byte[] plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/>. Engine has already stripped the magic byte
    /// prefix. Implementation is responsible for identifying which historical key in its ring
    /// the ciphertext was sealed with (typical schemes embed key id in the blob or in AAD).
    /// Throws on tampering, unknown key, or corruption — engine surfaces the exception so the
    /// caller can retry or alert.
    /// </summary>
    byte[] Unprotect(byte[] ciphertext);
}
