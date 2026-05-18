using Microsoft.AspNetCore.Mvc;

namespace MitLicenseCenter.Web.Endpoints;

// Заводим machine-readable code'ы для 409-конфликтов отдельной константой —
// frontend ориентируется на них, чтобы переводить ошибку в локализованное
// сообщение и помечать конкретное поле формы.
public static class ProblemCodes
{
    public const string NameDuplicate = "NAME_DUPLICATE";
    public const string TenantHasInfobases = "TENANT_HAS_INFOBASES";
    public const string NameDuplicateInTenant = "NAME_DUPLICATE_IN_TENANT";
}

public static class Problems
{
    public static ProblemDetails Conflict(string code, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://mitlicense.center/problems/conflict",
            Title = title,
            Status = StatusCodes.Status409Conflict,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    public static ProblemDetails TenantNameDuplicate(string name) =>
        Conflict(
            ProblemCodes.NameDuplicate,
            "Дубликат названия клиента",
            $"Клиент с названием «{name}» уже существует.");

    public static ProblemDetails TenantHasInfobases() =>
        Conflict(
            ProblemCodes.TenantHasInfobases,
            "Удаление невозможно",
            "У клиента есть инфобазы — сначала удалите их.");

    public static ProblemDetails InfobaseNameDuplicateInTenant(string name) =>
        Conflict(
            ProblemCodes.NameDuplicateInTenant,
            "Дубликат названия инфобазы",
            $"Инфобаза с названием «{name}» уже существует у этого клиента.");
}
