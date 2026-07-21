using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions;

/// <summary>
/// Contract of definition-level global variables: a JSON object the author attaches to a
/// workflow definition (environment URLs, feature toggles, sub-workflow ids, …). At run start
/// the runner freezes it into <c>static_context</c> under <see cref="RootKey"/> — the same
/// snapshot semantics as the graph itself, because a graph and its globals are authored as one
/// consistent pair. Expressions read them as <c>globals.&lt;key&gt;</c> in every engine.
/// <para>
/// NOT a secrets store: the column is plaintext by design — the absence of encryption is itself
/// the contract that nothing secret belongs here. A secret-bearing mechanism needs live reads
/// (rotation must reach in-flight runs) plus masked read APIs, and will be a separate feature.
/// </para>
/// </summary>
public static class WorkflowGlobals
{
    /// <summary>Static-context / expression-model root the runner writes globals under.</summary>
    public const string RootKey = "globals";

    /// <summary>
    /// Write-boundary guard: <paramref name="globals"/> must be a JSON object whose keys all
    /// pass <see cref="IsValidKey"/>. Throws <see cref="ArgumentException"/> otherwise — bad
    /// shapes are rejected at the store so the runner never has to re-validate persisted data.
    /// </summary>
    public static void EnsureValid(JsonElement globals)
    {
        if (globals.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"Definition globals must be a JSON object (got {globals.ValueKind}).", nameof(globals));
        }

        foreach (var prop in globals.EnumerateObject())
        {
            if (!IsValidKey(prop.Name))
            {
                throw new ArgumentException(
                    $"Definition globals key '{prop.Name}' is not a valid identifier " +
                    "([A-Za-z_][A-Za-z0-9_]*) — required so globals.<key> dot-access works in " +
                    "Liquid and JS expressions.",
                    nameof(globals));
            }
        }
    }

    /// <summary>ASCII identifier: letter or underscore first, letters / digits / underscores after.</summary>
    public static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (key[0] != '_' && !char.IsAsciiLetter(key[0])) return false;
        for (var i = 1; i < key.Length; i++)
        {
            if (key[i] != '_' && !char.IsAsciiLetterOrDigit(key[i])) return false;
        }

        return true;
    }
}
