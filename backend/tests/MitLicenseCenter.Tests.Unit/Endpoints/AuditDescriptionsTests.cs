using FluentAssertions;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-021 — каталог формулировок аудита вынесен из инлайн-строк эндпоинтов в единый
// класс, чтобы их можно было протестировать как единицу и зафиксировать против дрейфа.
// Тексты должны совпадать дословно с историческими (рефакторинг 1:1).
public sealed class AuditDescriptionsTests
{
    [Fact]
    public void Tenant_descriptions_match_expected()
    {
        AuditDescriptions.TenantCreated("Acme", "admin")
            .Should().Be("Клиент «Acme» создан администратором admin.");
        AuditDescriptions.TenantUpdated("Acme", "admin")
            .Should().Be("Клиент «Acme» обновлён администратором admin.");
        AuditDescriptions.TenantDeleted("Acme", "admin")
            .Should().Be("Клиент «Acme» удалён администратором admin.");
    }

    [Fact]
    public void Infobase_descriptions_match_expected()
    {
        AuditDescriptions.InfobaseCreated("Бухгалтерия", "admin")
            .Should().Be("Инфобаза «Бухгалтерия» создана администратором admin.");
        AuditDescriptions.InfobaseUpdated("Бухгалтерия", "admin")
            .Should().Be("Инфобаза «Бухгалтерия» обновлена администратором admin.");
        AuditDescriptions.InfobaseDeleted("Бухгалтерия", "admin")
            .Should().Be("Инфобаза «Бухгалтерия» удалена администратором admin.");
        AuditDescriptions.InfobaseReassigned("Бухгалтерия", "Acme", "Beta", "admin")
            .Should().Be("Инфобаза «Бухгалтерия» перенесена от клиента «Acme» к клиенту «Beta» администратором admin.");
    }

    [Fact]
    public void Publication_in_infobase_aggregate_descriptions_match_expected()
    {
        AuditDescriptions.PublicationCreatedForInfobase("Default Web Site/ib", "Бухгалтерия", "admin")
            .Should().Be("Публикация «Default Web Site/ib» создана для инфобазы «Бухгалтерия» администратором admin.");
        AuditDescriptions.PublicationUpdatedForInfobase("Default Web Site/ib", "Бухгалтерия", "admin")
            .Should().Be("Публикация «Default Web Site/ib» обновлена для инфобазы «Бухгалтерия» администратором admin.");
        AuditDescriptions.PublicationDeletedWithInfobase("Default Web Site/ib", "Бухгалтерия", "admin")
            .Should().Be("Публикация «Default Web Site/ib» удалена вместе с инфобазой «Бухгалтерия» администратором admin.");
    }

    [Fact]
    public void Direct_publication_update_omits_infobase_clause()
    {
        // Прямое редактирование (PublicationsEndpoints) — без «для инфобазы …».
        AuditDescriptions.PublicationUpdated("Default Web Site/ib", "admin")
            .Should().Be("Публикация «Default Web Site/ib» обновлена администратором admin.");
    }

    [Fact]
    public void Published_mentions_webinst()
    {
        AuditDescriptions.PublicationPublished("Default Web Site/ib", "admin")
            .Should().Be("Публикация «Default Web Site/ib» опубликована через webinst администратором admin.");
    }

    [Fact]
    public void PlatformChanged_mentions_both_versions()
    {
        AuditDescriptions.PublicationPlatformChanged(
                "Default Web Site/ib", "8.3.23.1865", "8.3.24.1234", "admin")
            .Should().Be("Платформа публикации «Default Web Site/ib» изменена с 8.3.23.1865 на 8.3.24.1234 администратором admin.");
    }
}
