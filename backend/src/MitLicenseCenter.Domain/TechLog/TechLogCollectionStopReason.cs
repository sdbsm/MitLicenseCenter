namespace MitLicenseCenter.Domain.TechLog;

// Причина остановки сбора ТЖ. Заполнена только для Stopped: Manual — штатное снятие оператором/
// сторожем; TimeLimit/DiskLimit — авто-стоп по политике безопасности (фактически заполняет MLC-231,
// но все три объявлены сейчас, чтобы не менять frozen-int enum позже). Для Active/Interrupted — null
// (Interrupted не «остановлено по причине», а оборвано рестартом — это несёт сам Status).
// Целочисленные значения ЗАМОРОЖЕНЫ — контракт с БД HasConversion<int>: не переиспользовать и не
// переназначать. Новые члены добавляются только в конец с явным числом.
public enum TechLogCollectionStopReason
{
    Manual = 0,
    TimeLimit = 1,
    DiskLimit = 2,
}
