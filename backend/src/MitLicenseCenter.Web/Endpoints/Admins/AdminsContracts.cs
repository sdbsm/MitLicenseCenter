using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-058 — контракты раздела «Администраторы». Работают над Identity-учётками
// (AppUser/роли), а не над доменной сущностью, поэтому ответы собираются вручную из
// UserManager, без EF-mapping-хелпера.

// Одна учётка панели в списке. `Roles` — назначенные роли (Admin/Viewer); `IsActive` —
// не залочена ли учётка (Identity-lockout = «отключена»).
public sealed record AdminResponse(
    Guid Id,
    string UserName,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record AdminListResponse(IReadOnlyList<AdminResponse> Items);

// Создание учётки. Роль выбирается при создании (Admin/Viewer); смену роли у
// существующих учёток раздел НЕ поддерживает (вне объёма MLC-058). DataAnnotations —
// только для Swagger; реальную проверку делает обработчик (см. гочу в CLAUDE.md).
public sealed record CreateAdminRequest(
    [property: Required, StringLength(256, MinimumLength = 1)] string UserName,
    [property: Required] string Role);

// Ответ на создание/сброс пароля: сгенерированный временный пароль. Показывается в UI
// один раз; в аудит и логи не пишется.
public sealed record AdminCreatedResponse(Guid Id, string UserName, string GeneratedPassword);

public sealed record AdminPasswordResetResponse(string GeneratedPassword);
