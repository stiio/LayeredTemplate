using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Setup.OpenApi.Transformers;

/// <summary>
/// Fixes up the OpenAPI schema for polymorphic base types produced by <c>System.Text.Json</c>'s
/// source generator:
/// <list type="bullet">
/// <item><b>anyOf → oneOf</b> — STJ emits <c>anyOf + discriminator</c>; OpenAPI clients expect
///   <c>oneOf</c> for tagged unions. We flip the keyword.</item>
/// <item><b>Discriminator mapping with $refs</b> — default mapping values are bare discriminator
///   strings, not schema references; client generators can't resolve them. We rebuild the mapping
///   so each entry points at the derived type's <c>$ref</c>.</item>
/// <item><b>Discriminator property on the base schema</b> — STJ doesn't add the discriminator
///   property (e.g. <c>$type</c>) to <c>properties</c> on the base; only <c>required</c> mentions
///   it. We inject it as a string property with <c>enum</c>-of-all-discriminator-values so client
///   codegen sees a proper union-of-literal-types discriminator instead of an unenumerated string.</item>
/// </list>
/// </summary>
internal sealed class PolymorphismOneOfTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas is not { Count: > 0 } schemas)
        {
            return Task.CompletedTask;
        }

        // Snapshot the keys: the rename step adds/removes leaf schema keys while we iterate, so we
        // must not enumerate the live dictionary.
        foreach (var schemaName in schemas.Keys.ToArray())
        {
            if (!schemas.TryGetValue(schemaName, out var entry) || entry is not OpenApiSchema schema)
            {
                continue;
            }

            if (schema.Discriminator is null || schema.AnyOf is not { Count: > 0 })
            {
                continue;
            }

            schema.OneOf = schema.AnyOf;
            schema.AnyOf = null;

            // Rename first so the discriminator-on-base / require-on-leaves passes below operate on
            // the renamed mapping + leaf keys.
            RenameLeavesToCleanNames(schemaName, schema, schemas);
            AddDiscriminatorPropertyToBase(schema);
            RequireDiscriminatorOnLeaves(schema, schemas);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Declares the discriminator property (<c>type</c> / <c>kind</c>) on the BASE schema itself, as a
    /// string with an <c>enum</c> listing every derived-type discriminator value. STJ only lists the
    /// discriminator in the base's <c>required</c> and never adds it to <c>properties</c>, so without
    /// this the base renders as a bare <c>oneOf</c> wrapper whose discriminator is an unenumerated
    /// string. Adding it makes the base self-describing — codegen sees a proper literal-union
    /// discriminator and a usable base type. Values come from the discriminator mapping KEYS (same
    /// source the leaf loop uses), so order matches the mapping and stays deterministic. Idempotent.
    /// </summary>
    private static void AddDiscriminatorPropertyToBase(OpenApiSchema baseSchema)
    {
        var discriminator = baseSchema.Discriminator;
        if (discriminator?.PropertyName is not { Length: > 0 } propertyName || discriminator.Mapping is not { Count: > 0 } mapping)
        {
            return;
        }

        baseSchema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        if (baseSchema.Properties.ContainsKey(propertyName))
        {
            return;
        }

        baseSchema.Properties[propertyName] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = mapping.Keys.Select(value => JsonSerializer.SerializeToNode(value)!).ToList(),
        };
    }

    /// <summary>
    /// For each leaf in <paramref name="baseSchema"/>'s discriminator mapping, declare the
    /// discriminator property as a required, single-value <c>enum</c> string pinned to that leaf's
    /// discriminator value (the mapping KEY). Idempotent — safe across rebuilds and re-runs.
    /// </summary>
    private static void RequireDiscriminatorOnLeaves(OpenApiSchema baseSchema, IDictionary<string, IOpenApiSchema> schemas)
    {
        var discriminator = baseSchema.Discriminator;
        if (discriminator?.PropertyName is not { Length: > 0 } propertyName || discriminator.Mapping is not { Count: > 0 } mapping)
        {
            return;
        }

        foreach (var (discriminatorValue, reference) in mapping)
        {
            if (ResolveLeaf(reference, schemas) is not { } leaf)
            {
                continue;
            }

            leaf.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

            // The framework normally already emits the discriminator as a one-value enum string;
            // (re)write it deterministically so the leaf is self-describing even if it didn't.
            leaf.Properties[propertyName] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [JsonSerializer.SerializeToNode(discriminatorValue)!],
            };

            leaf.Required ??= new HashSet<string>(StringComparer.Ordinal);
            leaf.Required.Add(propertyName);
        }
    }

    /// <summary>
    /// Renames each polymorphic leaf schema from the framework default base-prefixed id
    /// (<c>FormElement</c> + <c>TextElement</c> = <c>FormElementTextElement</c>) to its plain class
    /// name (<c>TextElement</c>), repointing the base's <c>oneOf</c> entries and
    /// <c>discriminator.mapping</c> refs to match. The base prefix only disambiguates same-named
    /// leaves across DIFFERENT bases — our leaf type names are unique across the whole API. The
    /// framework names polymorphic derived types internally and ignores
    /// <c>CreateSchemaReferenceId</c> for them, so this post-hoc rename is the only hook that reaches
    /// them. Skips any leaf whose stripped name would collide with an existing schema (defensive).
    /// Only the base references its leaves (use-sites reference the base), so the base's own
    /// <c>oneOf</c> + <c>mapping</c> are the only refs to rewrite.
    /// </summary>
    private static void RenameLeavesToCleanNames(string baseName, OpenApiSchema baseSchema, IDictionary<string, IOpenApiSchema> schemas)
    {
        if (baseSchema.Discriminator?.Mapping is not { Count: > 0 } mapping)
        {
            return;
        }

        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in mapping.Values)
        {
            if (reference.Reference?.Id is not { Length: > 0 } oldId) continue;
            if (!oldId.StartsWith(baseName, StringComparison.Ordinal) || oldId.Length <= baseName.Length) continue;

            var newId = oldId[baseName.Length..];
            if (renames.ContainsKey(oldId) || schemas.ContainsKey(newId)) continue;

            renames[oldId] = newId;
        }

        if (renames.Count == 0)
        {
            return;
        }

        // 1. rename the schema component keys
        foreach (var (oldId, newId) in renames)
        {
            if (schemas.TryGetValue(oldId, out var leaf))
            {
                schemas[newId] = leaf;
                schemas.Remove(oldId);
            }
        }

        // 2. repoint the discriminator mapping refs
        foreach (var key in mapping.Keys.ToArray())
        {
            if (mapping[key].Reference?.Id is { } id && renames.TryGetValue(id, out var newId))
            {
                mapping[key] = new OpenApiSchemaReference(newId);
            }
        }

        // 3. repoint the oneOf member refs
        if (baseSchema.OneOf is { Count: > 0 })
        {
            baseSchema.OneOf = baseSchema.OneOf
                .Select(member => member is OpenApiSchemaReference reference
                    && reference.Reference?.Id is { } id
                    && renames.TryGetValue(id, out var newId)
                        ? (IOpenApiSchema)new OpenApiSchemaReference(newId)
                        : member)
                .ToList();
        }
    }

    /// <summary>
    /// Resolve a discriminator-mapping reference to its concrete <see cref="OpenApiSchema"/> in
    /// <paramref name="schemas"/> via the reference's component id.
    /// </summary>
    private static OpenApiSchema? ResolveLeaf(OpenApiSchemaReference reference, IDictionary<string, IOpenApiSchema> schemas)
    {
        if (reference.Reference?.Id is not { Length: > 0 } schemaId)
        {
            return null;
        }

        return schemas.TryGetValue(schemaId, out var leaf) && leaf is OpenApiSchema concrete
            ? concrete
            : null;
    }
}
