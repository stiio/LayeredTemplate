using System.Text;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// EF Core value converter wiring the <c>string</c> property side to <c>bytea</c> column side
/// with optional encryption. Behavior pivots on whether an <see cref="IWorkflowDataProtector"/>
/// was registered:
/// <list type="bullet">
///   <item>No protector → bytes are UTF-8 of the raw string. Reads decode as UTF-8 directly.</item>
///   <item>With protector → bytes are <c>[0x80 magic byte] || ciphertext</c>. Reads detect the
///   magic byte, strip it, and decrypt; rows that pre-date encryption (no magic byte) still
///   read fine as UTF-8 plaintext, supporting mixed-mode storage during a roll-out.</item>
/// </list>
/// <para>
/// 0x80 is a UTF-8 continuation byte — by definition never the first byte of valid UTF-8 text.
/// That's our discriminator: a row that starts with 0x80 is encrypted, anything else is
/// plaintext (or a corrupted encrypted blob, which fails loudly during decryption).
/// </para>
/// </summary>
internal sealed class WorkflowProtectedStringConverter : ValueConverter<string?, byte[]>
{
    /// <summary>UTF-8 continuation byte used as the engine's "encrypted blob" marker.</summary>
    public const byte EncryptedMagic = 0x80;

    public WorkflowProtectedStringConverter(IWorkflowDataProtector? protector)
        : base(
            v => ToProvider(v, protector),
            v => FromProvider(v, protector))
    {
    }

    private static byte[] ToProvider(string? value, IWorkflowDataProtector? protector)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<byte>();
        }

        var plaintext = Encoding.UTF8.GetBytes(value);
        if (protector is null)
        {
            return plaintext;
        }

        var ciphertext = protector.Protect(plaintext);
        // Single allocation: 1 byte for the magic + ciphertext payload.
        var output = new byte[1 + ciphertext.Length];
        output[0] = EncryptedMagic;
        Buffer.BlockCopy(ciphertext, 0, output, 1, ciphertext.Length);
        return output;
    }

    private static string FromProvider(byte[] data, IWorkflowDataProtector? protector)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        if (data[0] == EncryptedMagic)
        {
            if (protector is null)
            {
                // Encrypted bytes encountered but no protector configured — most likely the
                // operator removed the registration after rows were already encrypted. We can't
                // silently treat ciphertext as plaintext; that would deserialise garbage into
                // jsonb-typed contexts and corrupt downstream logic. Fail loud.
                throw new InvalidOperationException(
                    "Encountered encrypted workflow column but no IWorkflowDataProtector is registered. " +
                    "Re-register the protector or run a one-off decryption migration before disabling protection.");
            }

            var ciphertext = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, ciphertext, 0, ciphertext.Length);
            var plaintext = protector.Unprotect(ciphertext);
            return Encoding.UTF8.GetString(plaintext);
        }

        // No magic byte → plaintext UTF-8 (legacy row, or no-protector mode).
        return Encoding.UTF8.GetString(data);
    }
}
