using FluentAssertions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web;
using NetArchTest.Rules;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Architecture;

// MLC-030 / REF-02 — автоматические guard-тесты границ слоёв. До сих пор направление
// зависимостей (Web → Infrastructure → Application → Domain) и главная anti-corruption
// граница к 1С/IIS (ADR-5/16, расширенная ADR-20) держались только дисциплиной и код-ревью:
// ничто не падало, если кто-то заинжектит инфраструктурный адаптер прямо в эндпоинт. Эти
// тесты закрепляют инварианты в CI. Анализ — на уровне IL (NetArchTest/Mono.Cecil), поэтому
// ловит использование запрещённого типа даже в теле метода (напр. `new Process()` внутри
// хендлера), что недостижимо рефлексией по сигнатурам.
//
// ВАЖНО (правило 3): Web ЛЕГИТИМНО ссылается на сборку Infrastructure и на её НЕ-адаптерные
// неймспейсы — это разрешённый ADR-20 vertical slice к собственной БД панели:
//   • …Infrastructure.Persistence  (AppDbContext),
//   • …Infrastructure.Identity     (AppUser/AppRole/Roles),
//   • …Infrastructure.Audit        (сущность AuditLog через db.AuditLogs),
//   • …Infrastructure.Settings     (SettingsSeeder в fail-fast bootstrap),
//   • корневой …Infrastructure     (AddInfrastructure).
// Поэтому запрещаются НЕ вся сборка, а именно адаптерные неймспейсы 1С/IIS и внешние
// инфраструктурные типы — их Web обязан использовать только через Application-интерфейсы.
//
// Правило 4 («Web не обходит DI/интерфейсы для доступа к инфраструктуре») сознательно не
// выделено в отдельный тест: единственный реальный способ обойти DI — напрямую использовать
// адаптерный тип / Process / Microsoft.Web.Administration, что уже запрещает правило 3.
// Отдельная проверка без ложных срабатываний здесь невыразима.
public sealed class LayerBoundaryTests
{
    [Fact]
    public void Domain_has_no_dependency_on_other_layers()
    {
        var result = Types.InAssembly(typeof(Tenant).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "MitLicenseCenter.Application",
                "MitLicenseCenter.Infrastructure",
                "MitLicenseCenter.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain обязан оставаться без зависимостей от вышележащих слоёв (ADR-5). Нарушители: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_has_no_dependency_on_Infrastructure_or_Web()
    {
        var result = Types.InAssembly(typeof(IClusterClient).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "MitLicenseCenter.Infrastructure",
                "MitLicenseCenter.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application держит только интерфейсы/контракты и не должен знать об Infrastructure/Web (ADR-5/20). Нарушители: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Web_does_not_reference_OneC_IIS_infrastructure_adapters_directly()
    {
        // ACL к 1С/IIS (ADR-5/16/20): Web никогда не трогает rac.exe/ras.exe-адаптеры, IIS-адаптеры,
        // Microsoft.Web.Administration или кластерный System.Diagnostics.Process напрямую — только
        // через Application-интерфейсы (IClusterClient, IIisPublishingService, I*Discovery, I*Job…).
        // Запрещается тип System.Diagnostics.Process (НЕ весь неймспейс: Program.cs легитимно
        // использует System.Diagnostics.Activity для traceId).
        var result = Types.InAssembly(typeof(Program).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "MitLicenseCenter.Infrastructure.Clusters",
                "MitLicenseCenter.Infrastructure.Publishing",
                "MitLicenseCenter.Infrastructure.Discovery",
                "MitLicenseCenter.Infrastructure.Jobs",
                "MitLicenseCenter.Infrastructure.Performance",
                "Microsoft.Web.Administration",
                "System.Diagnostics.Process")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Web обязан ходить в инфраструктуру 1С/IIS только через Application-интерфейсы (ADR-5/16/20). Нарушители: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
