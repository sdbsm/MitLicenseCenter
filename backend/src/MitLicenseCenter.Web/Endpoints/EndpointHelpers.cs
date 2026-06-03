using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-021 — тонкие Web-хелперы, снимающие дублирование бойлерплейта в мутирующих
// эндпоинтах (initiator + uniqueness-backstop). Это рефакторинг внутри одного
// транспорта в духе vertical-slice (ADR-20): use-case-слой НЕ вводится, контракт
// (ProblemCodes/409, состав аудита) не меняется.
internal static class EndpointHelpers
{
    // Имя инициатора для записи аудита: имя аутентифицированного пользователя, либо
    // "unknown", если личность по какой-то причине не установлена. Единая точка вместо
    // повторяющегося `httpContext.User.Identity?.Name ?? "unknown"`.
    public static string ResolveInitiator(this HttpContext httpContext) =>
        httpContext.User.Identity?.Name ?? "unknown";

    // MLC-004/ADR-19 — backstop уникальности поверх предварительного AnyAsync.
    // Сохраняем изменения; если БД подняла нарушение уникального индекса (гонка двух
    // вставок, проскочивших pre-check), мапим его в задокументированный 409 ProblemDetails
    // по per-call таблице соответствий. Маппинг именно per-call, потому что один и тот же
    // индекс IX_Infobases_TenantId_Name на create/update даёт NAME_DUPLICATE_IN_TENANT, а
    // на reassign — INFOBASE_NAME_TAKEN_IN_TARGET (ADR-19).
    //
    // Возвращает null при успешном сохранении и ProblemDetails при смапленном конфликте;
    // эндпоинт сам оборачивает его в TypedResults.Conflict(...), сохраняя свой точный
    // union-тип результата. Неузнанное (None) либо не перечисленное в mappings нарушение
    // пробрасывается дальше — глобальный UseExceptionHandler вернёт 500 ProblemDetails.
    public static async Task<ProblemDetails?> SaveWithUniquenessBackstopAsync(
        this AppDbContext db,
        CancellationToken ct,
        params (UniqueIndexViolation Index, Func<ProblemDetails> Problem)[] mappings)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return null;
        }
        catch (DbUpdateException ex)
        {
            var violation = DbUniqueViolation.Identify(ex);
            foreach (var (index, problem) in mappings)
            {
                if (index == violation)
                {
                    // ProblemDetails строим лениво — только на фактическом конфликте,
                    // а не на каждом happy-path-сохранении.
                    return problem();
                }
            }
            throw;
        }
    }
}
