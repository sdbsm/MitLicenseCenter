namespace MitLicenseCenter.Application.TechLog;

// Генератор целевого logcfg.xml (ядро MLC-230). Чистый C# без файловой системы — максимально
// юнит-тестируемый. По (сценарий, имя ИБ?, каталог сбора, history) собирает XML, который панель
// устанавливает в conf платформы. ЗАКОН по фактам стенда MLC-229 (40_TECHLOG §6/§8):
//   • format="json", location = каталог сбора, ограниченный history;
//   • НИКАКОГО фильтра по длительности (<ge property="Dur"/>) — он не работает для JSON-ТЖ 8.5
//     (ловит 0); объём режется ТОЛЬКО типом события и p:processName;
//   • при заданном имени ИБ шаблон ОБЯЗАН содержать <eq property="p:processName" value="<ИБ>"/>
//     (инвариант изоляции арендатора, 60_SAFETY №2);
//   • опознавательный маркер-комментарий — по нему сторож отличает «наш» конфиг от чужого.
public interface ILogcfgBuilder
{
    // Собирает целевой logcfg.xml. infobaseProcessName — имя ИБ для изоляции арендатора (p:processName);
    // null = весь кластер (без фильтра по ИБ). collectionLocation — каталог сбора (атрибут location).
    // historyHours — сколько часов хранить ТЖ (атрибут history).
    string Build(TechLogScenario scenario, string? infobaseProcessName, string collectionLocation, int historyHours);

    // Распознаёт «наш» logcfg по маркеру (для сторожа и идемпотентной сверки). content — текст
    // фактического logcfg.xml из conf; true — это конфиг, поставленный панелью.
    bool IsManaged(string? content);
}
