using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Чистая классификация состояния рестарта rphost ДО завершения процесса (MLC-220, ADR-56).
// Вынесена из OneCProcessRestartService под юнит-тесты (без I/O). Guard от переиспользования
// Pid: решает, можно ли завершать ОС-процесс, по его текущему имени.
internal static class OneCProcessRestartPolicy
{
    // Классификация ОС-процесса перед kill по имени (Pid уже прошёл whitelist rac).
    //   • имя null (процесс уже ушёл сам) → Restarted: нечего делать, идемпотентный успех;
    //   • имя ≠ rphost → PidReused: ОС переназначила Pid другому процессу, завершать НЕЛЬЗЯ;
    //   • имя == rphost → null: можно завершать (решение принимает вызывающий, выполняя kill).
    // Сравнение имени регистронезависимо (ProcessName ОС-нормализует, но страхуемся).
    public static OneCProcessRestartOutcome? ClassifyBeforeKill(string? osProcessName, string expectedName)
    {
        if (osProcessName is null)
        {
            // Процесс с этим Pid уже отсутствует — рестартить нечего (идемпотентность).
            return OneCProcessRestartOutcome.Restarted;
        }

        if (!string.Equals(osProcessName, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            // Имя не rphost — Pid переиспользован ОС, завершать чужой процесс запрещено.
            return OneCProcessRestartOutcome.PidReused;
        }

        // Имя rphost — kill разрешён (вызывающий выполняет завершение и верификацию).
        return null;
    }
}
