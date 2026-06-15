namespace MitLicenseCenter.Application.Sessions;

// Кэш факта потребления лицензий (ADR-48, MLC-166). Холодный тир пишет результат
// `rac session list --licenses` (множество id→licensed + признак доступности факта),
// горячий тир читает его, чтобы классифицировать сеансы без второго спавна rac.exe.
// Singleton — состояние переживает scoped-инвокации cold/hot-циклов.
//
// Контракт «отсутствие ключа = неизвестно»: если id нет в knownById, сеанс ещё не
// классифицирован фактом → LicenseStatus.Pending (свежий сеанс на горячем тире).
public interface ILicenseFactCache
{
    // Обновляет снимок факта. knownById: id сеанса → true (лицензионный) / false
    // (известен факту, но без лицензии). available: удалось ли получить факт в этом
    // холодном цикле (false ⇒ enforcement приостановлен, см. KillEnforcer).
    void Update(IReadOnlyDictionary<Guid, bool> knownById, bool available);

    // Текущий снимок факта (горячий тир читает на каждом тике).
    LicenseFactSnapshot Current();
}

// Иммутабельный снимок факта лицензий. Known(id) различает «известен факту»
// (true/false из knownById) и «неизвестен» (Pending).
public sealed class LicenseFactSnapshot
{
    public static readonly LicenseFactSnapshot Empty =
        new(new Dictionary<Guid, bool>(), available: false);

    private readonly IReadOnlyDictionary<Guid, bool> _knownById;

    public LicenseFactSnapshot(IReadOnlyDictionary<Guid, bool> knownById, bool available)
    {
        _knownById = knownById;
        Available = available;
    }

    // Доступен ли факт (false ⇒ панель честно показывает «данные о лицензиях недоступны»,
    // enforcement приостановлен).
    public bool Available { get; }

    // true — сеанс классифицирован фактом (значение licensed в out). false — id
    // неизвестен факту (Pending).
    public bool TryGet(Guid sessionId, out bool licensed)
        => _knownById.TryGetValue(sessionId, out licensed);

    // Классифицирует сеанс в трёхсостояние: неизвестен → Pending; известен →
    // Consuming/NotConsuming по факту.
    public LicenseStatus Classify(Guid sessionId)
        => _knownById.TryGetValue(sessionId, out var licensed)
            ? (licensed ? LicenseStatus.Consuming : LicenseStatus.NotConsuming)
            : LicenseStatus.Pending;
}
