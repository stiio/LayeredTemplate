using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredTemplate.Web.Converters;

/// <inheritdoc />
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.Parse(
            reader.GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var stringDate = value.ToString("O", CultureInfo.InvariantCulture);
        writer.WriteStringValue(stringDate);
    }
}