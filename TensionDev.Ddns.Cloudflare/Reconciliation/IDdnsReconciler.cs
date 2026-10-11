namespace TensionDev.Ddns.Cloudflare.Reconciliation;

public interface IDdnsReconciler
{
    Task ReconcileAsync(CancellationToken cancellationToken);
}
