using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Stamps <see cref="IHaveProtectedData.ProtectionVersion"/> on entities being saved when a
/// protector is configured. Active during inserts always; on updates only when at least one
/// protected (encryption-converter-mapped) property is itself being written. Without that
/// guard, an UPDATE that touches only unprotected columns (e.g., flipping <c>status</c>) would
/// stamp the current version onto a row whose ciphertext was actually written under an older
/// key — a confusing inconsistency for any later re-encryption sweep.
/// </summary>
internal sealed class WorkflowProtectionStampInterceptor : SaveChangesInterceptor
{
    private readonly IWorkflowDataProtector? protector;

    public WorkflowProtectionStampInterceptor(IWorkflowDataProtector? protector)
    {
        this.protector = protector;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.StampIfNeeded(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        this.StampIfNeeded(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void StampIfNeeded(DbContext? context)
    {
        if (this.protector is null || context is null)
        {
            return;
        }

        var version = this.protector.CurrentKeyVersion;
        foreach (var entry in context.ChangeTracker.Entries<IHaveProtectedData>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Brand new row — every protected property is being written.
                    entry.Entity.ProtectionVersion = version;
                    break;

                case EntityState.Modified:
                    // Stamp only when actual ciphertext is being rewritten. Reading a property
                    // and saving the entity unchanged in EF terms doesn't trigger this.
                    if (HasProtectedPropertyChange(entry))
                    {
                        entry.Entity.ProtectionVersion = version;
                    }
                break;
            }
        }
    }

    private static bool HasProtectedPropertyChange(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        // "Protected" = property has either of the engine's bytea-with-magic-byte converters
        // applied via HasConversion. Both share the same encryption envelope; checking either
        // type means "this column carries ciphertext when protection is on" — exactly the
        // columns whose key-version we care about.
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;
            var converter = prop.Metadata.GetValueConverter();
            if (converter is WorkflowProtectedStringConverter or WorkflowProtectedJsonConverter)
            {
                return true;
            }
        }
        return false;
    }
}
