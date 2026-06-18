using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Статус локальной службы SQL для агрегатора (MLC-213, только наблюдение — управление
// SQL вне объёма, ADR-54). Internal в Infrastructure. Обнаружение службы — по ImagePath,
// содержащему sqlservr.exe (через IServiceRegistryReader), состояние — через
// IServiceStateReader; имя инстанса — best-effort через ISqlInstanceDiscovery.
internal interface ISqlServiceStatusReader
{
    // Never-throws: любой сбой (реестр/ServiceController/инстанс) отражается флагом
    // Available:false + Error, а не исключением (вызывается из агрегатора).
    SqlStatusSummary Read();
}
