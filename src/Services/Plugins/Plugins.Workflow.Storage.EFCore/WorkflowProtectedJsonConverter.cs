using System.Text;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// EF Core value converter wiring a <c>JsonElement</c> property to a <c>bytea</c> column with
/// optional encryption. Mirror of <see cref="WorkflowProtectedStringConverter"/> for fields the
/// engine treats as JSON end-to-end (StaticContext, StepsOutputs, ResolvedConfig, Outputs,
/// ReturnValue) — keeping the property strongly-typed eliminates the runtime
/// <c>JsonSerializer.Deserialize</c> hot-path and removes a class of bugs where consumer code
/// could stuff malformed strings into the column.
/// <list type="bullet">
///   <item>No protector → bytes are UTF-8 of <c>JsonElement.GetRawText()</c>. Reads parse the
///   buffer back into a self-owning JsonElement (Clone() so the underlying JsonDocument can
///   be disposed).</item>
///   <item>With protector → bytes are <c>[0x80 magic byte] || ciphertext</c>. On read, the
///   magic byte is stripped, ciphertext decrypted, then parsed as JSON. Plaintext rows
///   (no magic byte) are still readable — supports mixed-mode during a key roll-out.</item>
/// </list>
/// <para>
/// Property side is nullable. Null is represented by an empty byte array, distinct from a
/// stored JSON <c>null</c> literal (which would be 4 bytes <c>"null"</c>). On read, an empty
/// buffer maps to <c>JsonElement?</c> = null rather than parsing failure.
/// </para>
/// </summary>
internal sealed class WorkflowProtectedJsonConverter : ValueConverter<JsonElement?, byte[]>
{
    /// <summary>Same magic byte as <see cref="WorkflowProtectedStringConverter"/> — keep them in sync.</summary>
    public const byte EncryptedMagic = WorkflowProtectedStringConverter.EncryptedMagic;

    public WorkflowProtectedJsonConverter(IWorkflowDataProtector? protector)
        : base(
            v => ToProvider(v, protector),
            v => FromProvider(v, protector))
    {
    }

    private static byte[] ToProvider(JsonElement? value, IWorkflowDataProtector? protector)
    {
        if (value is not { } el || el.ValueKind == JsonValueKind.Undefined)
        {
            return Array.Empty<byte>();
        }

        // GetRawText preserves the canonical JSON form the engine wrote, including key ordering
        // and number formatting. We don't re-serialize here; the input is already authoritative.
        var plaintext = Encoding.UTF8.GetBytes(el.GetRawText());
        if (protector is null)
        {
            return plaintext;
        }

        var ciphertext = protector.Protect(plaintext);
        var output = new byte[1 + ciphertext.Length];
        output[0] = EncryptedMagic;
        Buffer.BlockCopy(ciphertext, 0, output, 1, ciphertext.Length);
        return output;
    }

    private static JsonElement? FromProvider(byte[] data, IWorkflowDataProtector? protector)
    {
        if (data.Length == 0)
        {
            return null;
        }

        ReadOnlySpan<byte> jsonBytes;
        byte[]? decryptedBuffer = null;

        if (data[0] == EncryptedMagic)
        {
            if (protector is null)
            {
                throw new InvalidOperationException(
                    "Encountered encrypted workflow column but no IWorkflowDataProtector is registered. " +
                    "Re-register the protector or run a one-off decryption migration before disabling protection.");
            }

            var ciphertext = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, ciphertext, 0, ciphertext.Length);
            decryptedBuffer = protector.Unprotect(ciphertext);
            jsonBytes = decryptedBuffer;
        }
        else
        {
            jsonBytes = data;
        }

        // Parse into a fresh JsonDocument and Clone the root — the document owns the buffer
        // briefly via `using`, after Clone the returned element is detached and the document is
        // disposed safely. Without Clone the element would point into a buffer that's about to
        // be returned to the pool.
        using var doc = JsonDocument.Parse(jsonBytes.ToArray());
        return doc.RootElement.Clone();
    }
}
