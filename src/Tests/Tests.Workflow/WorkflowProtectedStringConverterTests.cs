using System.Text;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Round-trip semantics for <c>WorkflowProtectedStringConverter</c>:
///   - no protector → plaintext UTF-8 in / out
///   - with protector → magic-byte prefix + ciphertext, transparently encrypts on write and
///     decrypts on read
///   - mixed mode (protector enabled, legacy plaintext rows still in the table) → reads still
///     work because magic-byte detection falls back to UTF-8
///   - encryption removed but rows still encrypted → loud throw, no silent garbage.
/// </summary>
public class WorkflowProtectedStringConverterTests
{
    private const string Sample = "{\"answers\":{\"email\":\"x@y.com\"},\"meta\":{}}";

    [Fact]
    public void No_protector_round_trip_writes_utf8_plaintext()
    {
        var converter = new WorkflowProtectedStringConverter(protector: null);
        var bytes = ToProvider(converter, Sample);
        var first = bytes[0];

        Assert.Equal(Encoding.UTF8.GetBytes(Sample), bytes);
        // Plaintext must NEVER start with the magic byte; first byte of valid UTF-8 is < 0x80
        // (ASCII) or 0xC2-0xF4 (multi-byte leader).
        Assert.NotEqual(WorkflowProtectedStringConverter.EncryptedMagic, first);

        var roundTrip = FromProvider(converter, bytes);
        Assert.Equal(Sample, roundTrip);
    }

    [Fact]
    public void No_protector_empty_string_handled()
    {
        var converter = new WorkflowProtectedStringConverter(protector: null);
        var bytes = ToProvider(converter, string.Empty);
        Assert.Empty(bytes);

        var roundTrip = FromProvider(converter, bytes);
        Assert.Equal(string.Empty, roundTrip);
    }

    [Fact]
    public void With_protector_round_trip_uses_magic_byte_and_ciphertext()
    {
        var protector = new ReversingDataProtector();  // "encrypt" by reversing bytes
        var converter = new WorkflowProtectedStringConverter(protector);

        var bytes = ToProvider(converter, Sample);

        Assert.Equal(WorkflowProtectedStringConverter.EncryptedMagic, bytes[0]);
        // Ciphertext is reversed plaintext UTF-8.
        var plaintextUtf8 = Encoding.UTF8.GetBytes(Sample);
        Array.Reverse(plaintextUtf8);
        Assert.Equal(plaintextUtf8, bytes[1..]);

        var roundTrip = FromProvider(converter, bytes);
        Assert.Equal(Sample, roundTrip);
    }

    [Fact]
    public void Mixed_mode_legacy_plaintext_row_readable_when_protector_added_later()
    {
        // Simulate: the row was written when protector wasn't registered (UTF-8 plaintext).
        // Then we register a protector. Read should still return original — magic-byte
        // detection sees no 0x80 prefix and falls back to UTF-8 decode.
        var legacyPlaintextBytes = Encoding.UTF8.GetBytes(Sample);
        var converter = new WorkflowProtectedStringConverter(new ReversingDataProtector());

        var roundTrip = FromProvider(converter, legacyPlaintextBytes);
        Assert.Equal(Sample, roundTrip);
    }

    [Fact]
    public void Encrypted_row_without_protector_throws()
    {
        // Row written under protector (has magic byte). Operator removed protector
        // registration. On read we must fail loud — no silent garbage interpretation.
        var protector = new ReversingDataProtector();
        var converterWithProtector = new WorkflowProtectedStringConverter(protector);
        var encryptedBytes = ToProvider(converterWithProtector, Sample);

        var converterNoProtector = new WorkflowProtectedStringConverter(protector: null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FromProvider(converterNoProtector, encryptedBytes));
        Assert.Contains("encrypted workflow column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Reach into the converter's expression-compiled delegates via the same access EF uses.
    private static byte[] ToProvider(WorkflowProtectedStringConverter converter, string? value)
    {
        var fn = (Func<string?, byte[]>)converter.ConvertToProviderExpression.Compile();
        return fn(value);
    }

    private static string? FromProvider(WorkflowProtectedStringConverter converter, byte[] data)
    {
        var fn = (Func<byte[], string?>)converter.ConvertFromProviderExpression.Compile();
        return fn(data);
    }
}
