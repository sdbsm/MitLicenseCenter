using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-004 — какой уникальный индекс нарушен конкурентной вставкой.
// Предварительный AnyAsync в endpoint'ах остаётся быстрым happy-path'ом; этот
// enum используется только на backstop-пути, когда две гонящиеся транзакции
// проскочили проверку и БД подняла нарушение индекса как DbUpdateException.
internal enum UniqueIndexViolation
{
    None,
    InfobaseClusterId,        // IX_Infobases_ClusterInfobaseId (глобальная уникальность базы кластера)
    InfobaseTenantName,       // IX_Infobases_TenantId_Name (имя инфобазы уникально в пределах клиента)
    TenantName,               // IX_Tenants_Name (имя клиента уникально глобально)
    HiddenClusterInfobasePk,  // PK_HiddenClusterInfobases (повторный hide базы кластера, MLC-092)
}

// Распознаёт нарушение уникального индекса в DbUpdateException и сообщает, какой
// именно индекс пострадал, чтобы endpoint вернул задокументированный 409 с нужным
// ProblemCodes.* вместо голого 500.
internal static class DbUniqueViolation
{
    // SQL Server: 2601 — duplicate key (нарушение уникального индекса),
    // 2627 — нарушение уникального ограничения (constraint).
    private const int SqlDuplicateKeyIndex = 2601;
    private const int SqlUniqueConstraint = 2627;

    public static UniqueIndexViolation Identify(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null || !IsUniqueViolation(inner))
        {
            return UniqueIndexViolation.None;
        }

        // Имя нарушенного индекса присутствует в тексте SqlException дословно — это
        // стабильный идентификатор схемы (не локализованный человекочитаемый текст),
        // по нему и различаем, какой именно индекс пострадал.
        var message = inner.Message;
        if (message.Contains("IX_Infobases_ClusterInfobaseId", StringComparison.Ordinal))
        {
            return UniqueIndexViolation.InfobaseClusterId;
        }
        if (message.Contains("IX_Infobases_TenantId_Name", StringComparison.Ordinal))
        {
            return UniqueIndexViolation.InfobaseTenantName;
        }
        if (message.Contains("IX_Tenants_Name", StringComparison.Ordinal))
        {
            return UniqueIndexViolation.TenantName;
        }
        // MLC-092: у игнор-листа уникальность несёт сам PK (ClusterInfobaseId) — SQL Server
        // кладёт имя нарушенного constraint'а в текст так же, как имя индекса.
        if (message.Contains("PK_HiddenClusterInfobases", StringComparison.Ordinal))
        {
            return UniqueIndexViolation.HiddenClusterInfobasePk;
        }
        return UniqueIndexViolation.None;
    }

    private static bool IsUniqueViolation(Exception inner) =>
        inner is SqlException sql
            // Прод-путь (SQL Server): отличаем нарушение уникальности от прочих
            // SQL-ошибок по числовому коду, а не по тексту.
            ? sql.Number is SqlDuplicateKeyIndex or SqlUniqueConstraint
            // Иной провайдер либо искусственное исключение в тестах: класс ошибки
            // подтверждается стабильным именем индекса в сообщении (см. Identify).
            : true;
}
