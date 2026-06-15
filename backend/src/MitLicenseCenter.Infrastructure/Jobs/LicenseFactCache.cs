using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Infrastructure.Jobs;

// Реализация кэша факта лицензий (ADR-48, MLC-166). Singleton: холодный цикл пишет
// (Update), горячий читает (Current). Снимок иммутабельный и подменяется атомарно ссылкой
// под коротким замком — горячий тир получает целостный срез без частичного обновления.
internal sealed class LicenseFactCache : ILicenseFactCache
{
    private readonly object _gate = new();
    private LicenseFactSnapshot _current = LicenseFactSnapshot.Empty;

    public void Update(IReadOnlyDictionary<Guid, bool> knownById, bool available)
    {
        ArgumentNullException.ThrowIfNull(knownById);
        // Копируем в собственный словарь — вызывающий не должен мутировать снимок после
        // публикации (иммутабельность среза для горячего читателя).
        var snapshot = new LicenseFactSnapshot(
            new Dictionary<Guid, bool>(knownById), available);
        lock (_gate)
        {
            _current = snapshot;
        }
    }

    public LicenseFactSnapshot Current()
    {
        lock (_gate)
        {
            return _current;
        }
    }
}
