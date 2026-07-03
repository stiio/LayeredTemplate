using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// <see cref="WorkflowSignaler"/> behaviour — the generic signal-wait fan-out:
///  - fans out to ALL waiting runs on a key (delivered = N), payload reaches the resumer;
///  - tenant-isolated: a key in tenant A is invisible to a signal in tenant B;
///  - idempotent: a second signal after delivery finds no bookmarks (delivered = 0);
///  - stale: a bookmark whose step is no longer Waiting is counted Stale + deleted, never re-resumed;
///  - consumed (delivered + stale) bookmarks are eagerly deleted; genuinely-broken ones are left.
/// </summary>
public class WorkflowSignalerTests
{
    [Fact]
    public async Task Fans_out_to_all_waiting_runs_on_the_key()
    {
        var tenant = Guid.NewGuid();
        var b1 = Bookmark(tenant, "submission:1", "signalled");
        var b2 = Bookmark(tenant, "submission:1", "signalled");
        var store = MakeStore(b1, b2);
        var resumer = new FakeResumer(); // all succeed
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);

        var payload = JsonDocument.Parse("""{"answer":"yes"}""").RootElement;
        var result = await signaler.SignalAsync(tenant, "submission:1", payload, CancellationToken.None);

        Assert.Equal(2, result.Delivered);
        Assert.Equal(0, result.Stale);
        // Both exact frozen steps resumed on the bookmark's port, with the tenant param + payload.
        // (Each resume commits as its own atomic transaction inside the resumer — nothing to
        // assert at this seam since the flush parameter is gone.)
        Assert.Equal(2, resumer.Commands.Count);
        Assert.All(resumer.Commands, c => Assert.Equal(tenant, c.TenantId));
        Assert.All(resumer.Commands, c => Assert.Equal("signalled", c.Port));
        Assert.Contains(resumer.Commands, c => c.StepId == b1.StepId && c.RunId == b1.RunId);
        Assert.Contains(resumer.Commands, c => c.StepId == b2.StepId && c.RunId == b2.RunId);
        // Both consumed bookmarks eagerly deleted.
        Assert.Equal(new[] { b1.Id, b2.Id }.OrderBy(x => x), store.DeletedBookmarkIds.OrderBy(x => x));
    }

    [Fact]
    public async Task Tenant_isolated_lookup_returns_no_bookmarks_for_other_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        // The bookmark lives in tenant A; the store's FindBookmarksAsync is tenant-scoped, so a
        // signal in tenant B finds nothing.
        var store = MakeStore(Bookmark(tenantA, "K", "signalled"));
        var resumer = new FakeResumer();
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);

        var result = await signaler.SignalAsync(tenantB, "K", payload: null, CancellationToken.None);

        Assert.Equal(0, result.Delivered);
        Assert.Equal(0, result.Stale);
        Assert.Empty(resumer.Commands); // never reached the resumer
        Assert.Empty(store.DeletedBookmarkIds);
    }

    [Fact]
    public async Task Second_signal_after_delivery_finds_no_bookmarks()
    {
        var tenant = Guid.NewGuid();
        var store = MakeStore(Bookmark(tenant, "K", "signalled"));
        var resumer = new FakeResumer();
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);

        var first = await signaler.SignalAsync(tenant, "K", null, CancellationToken.None);
        Assert.Equal(1, first.Delivered);

        // Bookmark consumed (deleted) → re-signal finds nothing.
        var second = await signaler.SignalAsync(tenant, "K", null, CancellationToken.None);
        Assert.Equal(0, second.Delivered);
        Assert.Equal(0, second.Stale);
    }

    [Fact]
    public async Task Stale_bookmark_on_already_resumed_step_is_counted_and_deleted_without_double_resume()
    {
        var tenant = Guid.NewGuid();
        var b = Bookmark(tenant, "K", "signalled");
        var store = MakeStore(b);
        // Resumer reports the step is no longer Waiting (resumed elsewhere / timed out).
        var resumer = new FakeResumer(
            _ => WorkflowResumeResult.Failure(WorkflowResumeFailureReason.StepNotWaiting, "gone"));
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);

        var result = await signaler.SignalAsync(tenant, "K", null, CancellationToken.None);

        Assert.Equal(0, result.Delivered);
        Assert.Equal(1, result.Stale);
        Assert.Single(resumer.Commands); // attempted once, no double-resume
        Assert.Contains(b.Id, store.DeletedBookmarkIds); // stale bookmark reaped
    }

    [Fact]
    public async Task Genuinely_broken_bookmark_is_left_for_the_sweep()
    {
        var tenant = Guid.NewGuid();
        var b = Bookmark(tenant, "K", "signalled");
        var store = MakeStore(b);
        // RunNotFound (run purged mid-signal) — neither delivered nor stale; left for FK / sweep.
        var resumer = new FakeResumer(
            _ => WorkflowResumeResult.Failure(WorkflowResumeFailureReason.RunNotFound, "purged"));
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);

        var result = await signaler.SignalAsync(tenant, "K", null, CancellationToken.None);

        Assert.Equal(0, result.Delivered);
        Assert.Equal(0, result.Stale);
        Assert.Empty(store.DeletedBookmarkIds); // NOT eagerly deleted — reconciliation owns it
    }

    private static FakeStore MakeStore(params WorkflowBookmarkRecord[] bookmarks)
    {
        var store = new FakeStore();
        store.Bookmarks.AddRange(bookmarks);
        return store;
    }

    private static WorkflowBookmarkRecord Bookmark(Guid tenant, string key, string port) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenant,
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        CorrelationKey = key,
        ResumePort = port,
        CreatedAt = DateTime.UtcNow,
    };
}
