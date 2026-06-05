namespace MitLicenseCenter.Domain.Publications;

// Целочисленные значения зафиксированы — контракт с БД (HasConversion<int>)
// и UI (JsonStringEnumConverter сериализует именем). Read-only статус факта
// публикации в IIS (MLC-045), без сравнения с эталоном:
//   Published    — сайт IIS, виртуальный каталог и web.config на месте.
//   NotPublished — чего-то из этого физически нет (сайт/vdir/web.config).
//   Error        — адаптер не смог прочитать состояние (нет прав / COM / IO).
//   Unknown      — проверка ещё не выполнялась (дефолт).
public enum PublicationPublishStatus
{
    Unknown = 0,
    Published = 1,
    NotPublished = 2,
    Error = 3,
}
