using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredTemplate.Web.Api.Converters;

/// <inheritdoc />
public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    /// <inheritdoc />
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.Parse(reader.GetString()!);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        var stringDate = value.ToString("O", CultureInfo.InvariantCulture);
        writer.WriteStringValue(stringDate);
    }
}