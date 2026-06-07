namespace MitLicenseCenter.Application.Identity;

// MLC-058 — порт генерации временного пароля для администраторских операций
// (создание учётки / сброс пароля из веб-панели). Реализация в Infrastructure
// переиспользует единый генератор сидера (IdentitySeeder.GenerateInitialPassword),
// сохраняя парити с парольной политикой Identity без второго источника. Web инжектит
// интерфейс (ADR-20: к Infrastructure-internals напрямую не лезет).
public interface IInitialPasswordGenerator
{
    // Криптослучайный пароль, удовлетворяющий дефолтной политике Identity
    // (длина ≥ 12, минимум по одной заглавной/строчной/цифре/спецсимволу).
    string Generate();
}
