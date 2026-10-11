using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare.Tests;

internal sealed class StubReconciler(Func<CancellationToken, Task> action) : IDdnsReconciler
{
    public Task ReconcileAsync(CancellationToken cancellationToken) => action(cancellationToken);
}
