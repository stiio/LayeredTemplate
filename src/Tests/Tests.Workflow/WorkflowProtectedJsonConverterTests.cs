using System.Text;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Round-trip semantics for <c>WorkflowProtectedJsonConverter</c> — the <c>JsonElement</c>
/// mirror of the string converter (StaticContext, StepsOutputs, ResolvedConfig, Outputs,
/// ReturnValue columns). Same magic-byte contract; plus the null-vs-JSON-null distinction:
/// a null property maps to an EMPTY buffer, which reads back as null rather than a parse error.
/// </summary>
public class WorkflowProtectedJsonConverterTests
{
    private const string SampleJson = "{\"answers\":{\"email\":\"x@y.com\"},\"count\":3}";

    [Fact]
    public void No_protector_round_trip_writes_raw_json_utf8()
    {
        var converter = new WorkflowProtectedJsonConverter(protector: null);
        var element = JsonDocument.Parse(SampleJson).RootElement.Clone();

        var bytes = ToProvider(converter, element);
        Assert.Equal(Encoding.UTF8.GetBytes(SampleJson), bytes);

        var roundTrip = FromProvider(converter, bytes);
        Assert.NotNull(roundTrip);
        Assert.Equal(SampleJson, roundTrip!.Value.GetRawText());
    }

    [Fact]
    public void Null_value_maps_to_empty_buffer_and_back_to_null()
    {
        // Null property ↔ empty byte array — distinct from a stored JSON `null` literal
        // (which would be the 4 bytes "null").
        var converter = new WorkflowProtectedJsonConverter(protector: null);

        var bytes = ToProvider(converter, null);
        Assert.Empty(bytes);

        var roundTrip = FromProvider(converter, bytes);
        Assert.Null(roundTrip);
    }

    [Fact]
    public void With_protector_round_trip_uses_magic_byte_and_ciphertext()
    {
        var converter = new WorkflowProtectedJsonConverter(new ReversingDataProtector());
        var element = JsonDocument.Parse(SampleJson).RootElement.Clone();

        var bytes = ToProvider(converter, element);

        Assert.Equal(WorkflowProtectedJsonConverter.EncryptedMagic, bytes[0]);
        // Ciphertext is reversed raw-JSON UTF-8.
        var plaintextUtf8 = Encoding.UTF8.GetBytes(SampleJson);
        Array.Reverse(plaintextUtf8);
        Assert.Equal(plaintextUtf8, bytes[1..]);

        var roundTrip = FromProvider(converter, bytes);
        Assert.Equal(SampleJson, roundTrip!.Value.GetRawText());
    }

    [Fact]
    public void Mixed_mode_legacy_plaintext_row_readable_when_protector_added_later()
    {
        var legacyPlaintextBytes = Encoding.UTF8.GetBytes(SampleJson);
        var converter = new WorkflowProtectedJsonConverter(new ReversingDataProtector());

        var roundTrip = FromProvider(converter, legacyPlaintextBytes);
        Assert.Equal(SampleJson, roundTrip!.Value.GetRawText());
    }

    [Fact]
    public void Encrypted_row_without_protector_throws()
    {
        var converterWithProtector = new WorkflowProtectedJsonConverter(new ReversingDataProtector());
        var element = JsonDocument.Parse(SampleJson).RootElement.Clone();
        var encryptedBytes = ToProvider(converterWithProtector, element);

        var converterNoProtector = new WorkflowProtectedJsonConverter(protector: null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FromProvider(converterNoProtector, encryptedBytes));
        Assert.Contains("encrypted workflow column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Round_trip_returns_a_self_owning_element()
    {
        // FromProvider must Clone out of its short-lived JsonDocument — the element stays
        // readable after the converter returns (no disposed-buffer access).
        var converter = new WorkflowProtectedJsonConverter(protector: null);
        var element = JsonDocument.Parse(SampleJson).RootElement.Clone();

        var roundTrip = FromProvider(converter, ToProvider(converter, element));

        // Force full traversal — would throw if the element pointed into a recycled buffer.
        Assert.Equal("x@y.com", roundTrip!.Value.GetProperty("answers").GetProperty("email").GetString());
        Assert.Equal(3, roundTrip.Value.GetProperty("count").GetInt32());
    }

    // Reach into the converter's expression-compiled delegates via the same access EF uses.
    private static byte[] ToProvider(WorkflowProtectedJsonConverter converter, JsonElement? value)
    {
        var fn = (Func<JsonElement?, byte[]>)converter.ConvertToProviderExpression.Compile();
        return fn(value);
    }

    private static JsonElement? FromProvider(WorkflowProtectedJsonConverter converter, byte[] data)
    {
        var fn = (Func<byte[], JsonElement?>)converter.ConvertFromProviderExpression.Compile();
        return fn(data);
    }
}
