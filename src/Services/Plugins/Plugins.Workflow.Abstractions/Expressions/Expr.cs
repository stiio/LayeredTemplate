using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>
/// Dynamic-value wrapper for action config properties. Wire format in stored config:
/// <c>{ "engine": "static"|"liquid"|"js", "value": "..." }</c>. After the resolver runs the
/// instance carries the concrete <see cref="Resolved"/> value, which is what the action reads via
/// the implicit conversion to <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// Type rule: <typeparamref name="T"/> is a pure data type — it MUST NOT contain nested
/// <see cref="Expr{U}"/> fields. For nested dynamics use plain containers in TConfig
/// (e.g. <c>List&lt;HttpHeader&gt;</c> where <c>HttpHeader.Value</c> is <c>Expr&lt;string&gt;</c>).
/// </remarks>
[JsonConverter(typeof(ExprJsonConverterFactory))]
public sealed class Expr<T>
{
    /// <summary>One of <c>static</c>, <c>liquid</c>, <c>js</c>.</summary>
    public string Engine { get; set; } = ExpressionEngines.Static;

    /// <summary>The raw expression as written by the user. Static literals are also stored here.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The resolved value. Populated by the resolver; null until then.</summary>
    public T? Resolved { get; set; }

    /// <summary>Shortcut accessor for <see cref="Resolved"/>. Returns default(T) when not yet resolved.</summary>
    public T? ReadResolved() => this.Resolved;

    public static implicit operator T?(Expr<T>? expr) => expr is null ? default : expr.Resolved;
}

internal class ExprJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Expr<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var inner = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ExprJsonConverter<>).MakeGenericType(inner);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

/// <summary>
/// Wire format: <c>{ "engine": "...", "value": "...", "resolved": ... }</c>. Wrapper keys are
/// hard-coded lowercase in both directions so the persisted shape stays stable regardless of
/// what <see cref="JsonSerializerOptions"/> the caller hands in. The inner <see cref="Expr{T}.Resolved"/>
/// payload bypasses the caller's options entirely and goes through
/// <see cref="WorkflowJsonOptions.Default"/> — guarantees camelCase + enum-as-string for the
/// nested config shape no matter who's serializing the surrounding config POCO.
/// </summary>
internal class ExprJsonConverter<T> : JsonConverter<Expr<T>>
{
    public override Expr<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected object for Expr<{typeof(T).Name}>, got {reader.TokenType}.");
        }

        var expr = new Expr<T>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return expr;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();
            switch (prop)
            {
                case "engine":
                    expr.Engine = reader.GetString() ?? ExpressionEngines.Static;
                    break;
                case "value":
                    expr.Value = reader.GetString() ?? string.Empty;
                    break;
                case "resolved":
                    // Defensive bake: ignore caller options for the nested payload so the inner
                    // shape is always camelCase + enum-as-string. Outer options are still used
                    // for the wrapper-level token reads above (they're just primitives).
                    expr.Resolved = JsonSerializer.Deserialize<T>(ref reader, WorkflowJsonOptions.Default);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        throw new JsonException("Unexpected end of stream while reading Expr.");
    }

    public override void Write(Utf8JsonWriter writer, Expr<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("engine", value.Engine);
        writer.WriteString("value", value.Value);
        if (value.Resolved is not null)
        {
            writer.WritePropertyName("resolved");
            // Same defensive bake as Read — see remarks on the converter class.
            JsonSerializer.Serialize(writer, value.Resolved, WorkflowJsonOptions.Default);
        }
        writer.WriteEndObject();
    }
}
