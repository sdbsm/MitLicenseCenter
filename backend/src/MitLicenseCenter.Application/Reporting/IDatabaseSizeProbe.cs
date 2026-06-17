namespace MitLicenseCenter.Application.Reporting;

// Порт замера размеров баз SQL (MLC-185). Реализация — DatabaseSizeProbe
// (Infrastructure, чистый ADO.NET); в тестах — FakeDatabaseSizeProbe. Джоба сбора
// телеметрии (MLC-185c) зовёт только этот интерфейс — она не строит SQL и не знает
// про sys.master_files.
public interface IDatabaseSizeProbe
{
    // Показания по ВСЕМ пользовательским базам инстанса (database_id > 4) — выделенное
    // (allocated) место в БАЙТАХ, отдельно данные и лог. Фильтрацию по базам инфобаз и
    // привязку к тенанту делает джоба сбора (MLC-185c), не адаптер. «Never throws»: при
    // недоступности SQL / отсутствии строки подключения → ПУСТОЙ список (деградированный
    // результат, как SqlBackupAdapter), отмена (OperationCanceledException) пробрасывается.
    Task<IReadOnlyList<DatabaseSizeReading>> ReadSizesAsync(CancellationToken ct);
}

// Одно показание размера базы: имя + выделенные байты данных (ROWS) и лога (LOG).
// Total не несём — это вычисляемое в отчёте/UI (как и в DatabaseSizeSnapshot).
public sealed record DatabaseSizeReading(string DatabaseName, long DataBytes, long LogBytes);
