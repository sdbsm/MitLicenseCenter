using Microsoft.AspNetCore.Identity;

namespace MitLicenseCenter.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    // MLC-059 — форс-смена пароля при первом входе. Создание/сброс учётки (раздел
    // «Администраторы») ставит флаг; вход обязывает сменить временный пароль, успешная
    // смена через /auth/change-password снимает его.
    public bool MustChangePassword { get; set; }

    // MLC-059 — время последнего успешного входа (UTC, пишется LoginAsync). null —
    // пользователь ни разу не входил. Показывается в списке администраторов.
    public DateTime? LastLoginAt { get; set; }
}
