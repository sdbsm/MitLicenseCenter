using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Сущность-телеметрия (MLC-070, ADR-26, Фаза 4): одно расследование быстродействия «по требованию».
// Живёт в Infrastructure.Reporting рядом с LicenseUsageSnapshot — это запись измерений, читаемая Web
// напрямую через AppDbContext (vertical slice ADR-20), а не доменный агрегат. Намеренно НЕ в
// Infrastructure.Performance: тот неймспейс — адаптерный (host/SQL-пробы, сервис записи), Web его не
// касается (guard LayerBoundaryTests). Активная запись (Status=Active) сэмплится фоновым таймером, пока
// оператор не остановит (StopReason=Manual) или не сработает авто-стоп (TimeLimit/SampleLimit); на
// рестарте процесса осиротевшая активная запись закрывается как Interrupted. Конфиг — inline в
// AppDbContext.OnModelCreating (как LicenseUsageSnapshot).
public sealed class PerfRecording : IEntity
{
    public Guid Id { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? StoppedAtUtc { get; set; }
    public PerfRecordingStatus Status { get; set; }
    // Причина остановки: заполнена только для Stopped; null для Active/Interrupted.
    public PerfRecordingStopReason? StopReason { get; set; }
    // Логин оператора, запустившего запись. Не аудируется отдельно (ADR-26), но фиксируется здесь.
    public string StartedBy { get; init; } = string.Empty;

    public ICollection<PerfRecordingSample> Samples { get; } = new List<PerfRecordingSample>();
}
