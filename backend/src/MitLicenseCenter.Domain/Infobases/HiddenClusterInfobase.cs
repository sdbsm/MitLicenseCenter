namespace MitLicenseCenter.Domain.Infobases;

// MLC-092 — игнор-лист «нераспределённых» баз кластера: служебные базы, которые
// оператор сознательно не заводит в панель (и не хочет видеть в баннере-счётчике).
// PK — сам ClusterInfobaseId (одна запись на базу кластера, без суррогатного Id).
// Name — снапшот имени на момент скрытия: блок «Скрытые» рендерится из БД даже
// когда RAS недоступен. Запись удаляется при unhide и при создании Infobase с этим
// ClusterInfobaseId (база перестала быть «нераспределённой» по определению).
public sealed class HiddenClusterInfobase
{
    public Guid ClusterInfobaseId { get; init; }
    public required string Name { get; init; }
    public DateTime HiddenAtUtc { get; init; }
    public required string HiddenBy { get; init; }
}
