using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredTemplate.Plugins.Workflow.Abstractions;

/// <summary>
/// Canonical <see cref="JsonSerializerOptions"/> for engine-internal JSON serialization —
/// resolved-config snapshots, static_context payloads, steps_outputs blobs, graph snapshots,
/// the inner <c>T</c> of <c>Expr&lt;T&gt;.Resolved</c>. Built on
/// <see cref="JsonSerializerDefaults.Web"/> (camelCase property names + case-insensitive reads)
/// plus a <see cref="JsonStringEnumConverter"/> with a camelCase enum-value policy so any
/// future enum field on a config POCO is stored as a stable readable string instead of an
/// integer.
/// <para>
/// Rule: every persistence-bound serialize / deserialize in the workflow plugins (engine,
/// storage, abstractions) goes through this instance. Constructing options ad-hoc (e.g.
/// <c>new JsonSerializerOptions(JsonSerializerDefaults.Web)</c>) silently drops the enum
/// converter and re-introduces wire-format drift between writers and readers. If you need
/// to extend the contract — additional converters, a different ignore-condition — modify
/// this class so every site picks the change up at once.
/// </para>
/// </summary>
public static class WorkflowJsonOptions
{
    /// <summary>
    /// Process-wide singleton. <see cref="JsonSerializer"/> caches type metadata against the
    /// options instance, so reusing one instance is the right thing performance-wise. Marked
    /// read-only: do <b>not</b> mutate after first use — System.Text.Json freezes options on
    /// first serialize and throws on subsequent mutation.
    /// </summary>
    public static readonly JsonSerializerOptions Default = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // CamelCase enum policy keeps stored values readable + greppable (e.g. "fastOnly"
        // instead of "FastOnly" or "0"). Authors of new action configs get this behaviour
        // for free — no per-property attribute needed.
        opts.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        opts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opts.WriteIndented = false;

        return opts;
    }
}
