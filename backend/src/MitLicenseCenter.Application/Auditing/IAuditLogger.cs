using MitLicenseCenter.Domain.Audit;

namespace MitLicenseCenter.Application.Auditing;

public interface IAuditLogger
{
    Task LogAsync(
        AuditActionType action,
        string initiator,
        string description,
        Guid? tenantId = null,
        AuditReason? reason = null,
        CancellationToken ct = default);

    // MLC-119 (BE-01/BE-25) — enlist-примитив: добавляет аудит-запись в общий tracked-контекст
    // БЕЗ собственного SaveChanges. Запись коммитится тем же SaveChangesAsync, что и сама
    // операция (атомарность «оба или ничего»), либо одним батч-SaveChanges на несколько записей.
    void Enlist(
        AuditActionType action,
        string initiator,
        string description,
        Guid? tenantId = null,
        AuditReason? reason = null);
}
