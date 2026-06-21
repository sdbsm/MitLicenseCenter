namespace MitLicenseCenter.Domain.TechLog;

// Причина остановки сбора «Дела» расследования (MLC-237, этап C). Заполнена только для Completed:
// Manual — штатное снятие оператором/сторожем; TimeLimit/DiskLimit — авто-стоп по политике безопасности
// (60_SAFETY №3, MLC-231); Error — сбор/разбор упал (наполняет MLC-238). Для Collecting/Analyzing/
// Interrupted — null (Interrupted не «остановлено по причине», а оборвано рестартом — это несёт сам Status).
//
// Целочисленные значения ЗАМОРОЖЕНЫ — контракт с БД HasConversion<int>: не переиспользовать и не
// переназначать. Значения 0/1/2 СОВМЕСТИМЫ со старым TechLogCollectionStopReason (Manual/TimeLimit/
// DiskLimit) — миграция данных переносит их 1:1. Error=3 — новое значение для оркестрации MLC-238.
public enum InvestigationStopReason
{
    Manual = 0,
    TimeLimit = 1,
    DiskLimit = 2,

    // Сбор/разбор завершился ошибкой (MLC-238). Объявлено заранее (frozen-int дисциплина).
    Error = 3,
}
