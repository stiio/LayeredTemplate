using System.IO.Hashing;
using System.Runtime.InteropServices;
using Fluid;
using Microsoft.Extensions.Caching.Memory;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;

/// <summary>
/// Singleton cache for compiled <see cref="IFluidTemplate"/>s, keyed by a non-cryptographic
/// hash of the template source. Backed by <see cref="MemoryCache"/> with size + sliding-expiration
/// eviction so a long-running process can't accumulate templates without bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why hashed keys.</b> The previous <c>ConcurrentDictionary&lt;string, IFluidTemplate&gt;</c>
/// retained a copy of every template source string as its key — for a 5KB email template times
/// 10k entries that's ~50MB of duplicated strings on top of the compiled trees. Hashing the
/// source down to a <see cref="ulong"/> trades a one-time per-lookup hash cost (a couple
/// microseconds for typical templates via xxHash64) for fixed-size keys and faster equality
/// comparisons inside the cache.
/// </para>
/// <para>
/// xxHash64 is a non-cryptographic hash. Collision risk is purely a cache-correctness concern,
/// not a security one (an attacker engineering a colliding template doesn't gain anything —
/// they'd just get someone else's compiled template, which they could've authored themselves).
/// At <see cref="MaxCachedTemplates"/> = 1000 entries the birthday-bound collision probability
/// is ~3 × 10⁻¹⁴ — orders of magnitude below "ECC bit-flip in RAM" levels.
/// </para>
/// <para>
/// <b>Why MemoryCache.</b> Compiled <see cref="IFluidTemplate"/>s are in-memory object graphs
/// holding delegate references to DI-resolved filters and <c>MemberAccessStrategy</c>
/// registrations — fundamentally non-serialisable, so distributed caching doesn't fit. The
/// only meaningful eviction policy is local LRU + idle-timeout, which is exactly what
/// <see cref="MemoryCache"/> provides via <see cref="MemoryCacheOptions.SizeLimit"/> +
/// <see cref="MemoryCacheEntryOptions.SlidingExpiration"/>.
/// </para>
/// </remarks>
internal sealed class LiquidTemplateCache : ILiquidTemplateCache, IDisposable
{
    /// <summary>
    /// Maximum number of distinct compiled templates retained. When exceeded, MemoryCache
    /// compaction evicts the least-recently-accessed
    /// <see cref="MemoryCacheOptions.CompactionPercentage"/> portion. 1000 fits typical
    /// orchestration-heavy tenants (~10 templates per workflow × ~100 workflows) without
    /// flooding the heap; bump if observed cache-miss rate climbs.
    /// </summary>
    private const int MaxCachedTemplates = 1000;

    /// <summary>
    /// Idle eviction window — a template not accessed for this long is dropped on the next
    /// MemoryCache scan. Keeps long-running processes from holding compiled trees for graphs
    /// that have been edited / deleted.
    /// </summary>
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// On overflow, evict 25% of entries (oldest by last access). Higher = fewer compactions
    /// but bigger cliffs; lower = more frequent compaction work. 25% is the framework default
    /// and a sane sweet-spot.
    /// </summary>
    private const double CompactionPercentage = 0.25;

    private readonly FluidParser parser = new();
    private readonly MemoryCache cache;

    public LiquidTemplateCache()
    {
        // Owned MemoryCache — registered as singleton so the cache survives the process. We
        // construct it ourselves rather than reusing the consumer's IMemoryCache to keep the
        // size budget local: a shared cache could starve other consumers (or vice-versa) when
        // workflow templates churn.
        this.cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = MaxCachedTemplates,
            CompactionPercentage = CompactionPercentage,
        });
    }

    public IFluidTemplate GetOrCompile(string template)
    {
        var key = ComputeKey(template);

        if (this.cache.TryGetValue(key, out IFluidTemplate? cached) && cached is not null)
        {
            return cached;
        }

        if (!this.parser.TryParse(template, out var parsed, out var error))
        {
            throw new InvalidOperationException($"Liquid parse error: {error}");
        }

        // Each entry counts as 1 against SizeLimit — we're capping by entry count, not by
        // serialized template bytes. Compiled trees vary wildly in size so an entry-count cap
        // is easier to reason about than a byte-budget.
        this.cache.Set(key, parsed, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = SlidingExpiration,
        });

        return parsed;
    }

    /// <summary>
    /// xxHash64 of the template's UTF-16 bytes. We hash the in-memory string representation
    /// directly via <see cref="MemoryMarshal.AsBytes"/> — no UTF-8 round-trip, no allocation.
    /// Identical strings always hash to the same key (deterministic across runs).
    /// </summary>
    private static ulong ComputeKey(string template) =>
        XxHash64.HashToUInt64(MemoryMarshal.AsBytes(template.AsSpan()));

    public void Dispose()
    {
        this.cache.Dispose();
    }
}
