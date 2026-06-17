using MitLicenseCenter.Application.Reporting;

namespace MitLicenseCenter.Infrastructure.Reporting.Testing;

// Программируемый фейк порта замера размеров баз для unit-тестов (образец
// FakeSqlBackupService): настраиваемый список показаний + запись числа вызовов — на нём
// строятся тесты джобы сбора телеметрии (MLC-185c): фильтрация по базам инфобаз, привязка
// к тенанту, запись DatabaseSizeSnapshot. В production-DI не регистрируется — реальный
// DatabaseSizeProbe ходит в SQL.
internal sealed class FakeDatabaseSizeProbe : IDatabaseSizeProbe
{
    // Управляемый результат замера. По умолчанию пуст («сервис не смог» / нет баз) —
    // как реальный адаптер при недоступном SQL. Тест задаёт показания через NextReadings.
    public IReadOnlyList<DatabaseSizeReading> NextReadings { get; set; } = [];

    // Сколько раз джоба дёрнула проб (для проверки, что замер вообще случился).
    public int ReadCount { get; private set; }

    public Task<IReadOnlyList<DatabaseSizeReading>> ReadSizesAsync(CancellationToken ct)
    {
        ReadCount++;
        return Task.FromResult(NextReadings);
    }
}
