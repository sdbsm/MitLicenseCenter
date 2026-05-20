namespace MitLicenseCenter.Application.Jobs;

public interface IReconciliationJob
{
    Task RunColdAsync(CancellationToken ct);
}
