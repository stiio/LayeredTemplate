using OpenIddict.Abstractions;

namespace LayeredTemplate.Auth.Web.Components.Admin.Applications;

internal static class AdminScopeHelper
{
    public static async Task<List<string>> ListScopeNamesAsync(
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken = default)
    {
        var names = new List<string>();
        await foreach (var scope in scopeManager.ListAsync(count: null, offset: null, cancellationToken))
        {
            var name = await scopeManager.GetNameAsync(scope, cancellationToken);
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>Keeps only scopes present in the registered list — protects against crafted POSTs.</summary>
    public static List<string> FilterToKnown(IEnumerable<string> submitted, IReadOnlyList<string> known) =>
        submitted.Intersect(known, StringComparer.Ordinal).ToList();
}
