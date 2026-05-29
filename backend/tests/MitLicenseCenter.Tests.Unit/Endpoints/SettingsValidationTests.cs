using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Settings;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Стиль Stage 2: invoke internal static handler напрямую, без WebApplicationFactory.
// SettingsStore собран на in-memory DbContext + EphemeralDataProtectionProvider,
// audit logger — capturing fake из TestHelpers.
public sealed class SettingsValidationTests
{
    private static (SettingsStore Store, TestHelpers.CapturingAuditLogger Audit, AppDbContext Db) MakeFixture()
    {
        var db = TestHelpers.NewInMemoryDb();
        var snapshot = Substitute.For<ISettingsSnapshot>();
        var store = new SettingsStore(db, new EphemeralDataProtectionProvider(), snapshot, TimeProvider.System);
        var audit = new TestHelpers.CapturingAuditLogger();
        return (store, audit, db);
    }

    [Fact]
    public async Task Unknown_key_returns_NotFound_with_SETTING_UNKNOWN_KEY()
    {
        var (store, audit, _) = MakeFixture();

        var result = await SettingsEndpoints.UpdateAsync(
            "Bogus.Key",
            new UpdateSettingRequest("anything"),
            store,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFound<ProblemDetails>>().Subject;
        notFound.Value!.Extensions["code"].Should().Be(ProblemCodes.SettingUnknownKey);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Out_of_range_numeric_returns_ValidationProblem_with_SETTING_INVALID_VALUE()
    {
        var (store, audit, _) = MakeFixture();

        var result = await SettingsEndpoints.UpdateAsync(
            SettingKey.PollingHotIntervalSeconds,
            new UpdateSettingRequest("999"),  // out of [2,60]
            store,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Extensions["code"].Should().Be(ProblemCodes.SettingInvalidValue);
        problem.ProblemDetails.Errors.Should().ContainKey(nameof(UpdateSettingRequest.Value));
        audit.Entries.Should().BeEmpty();
    }

    // Stage 5 PR 5.1 (ADR-16): SettingValueKind.Url-валидация осталась в коде
    // endpoint'а, но ни один ключ в catalog'е больше не использует Url-тип
    // (OneC.Cluster.RestApiUrl был единственным). Тест на invalid URL удалён;
    // первое же возвращение URL-ключа в catalog должно принести покрытие обратно.

    [Fact]
    public async Task Valid_secret_update_returns_Ok_and_audit_description_omits_plaintext()
    {
        var (store, audit, _) = MakeFixture();

        var result = await SettingsEndpoints.UpdateAsync(
            SettingKey.OneCClusterAdminPassword,
            new UpdateSettingRequest("topsecret-value"),
            store,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        audit.Entries.Should().ContainSingle();
        var entry = audit.Entries.Single();
        entry.Action.Should().Be(AuditActionType.SettingChanged);
        entry.Description.Should().NotContain("topsecret-value", "plaintext секрета никогда не уходит в audit description");
        entry.Description.Should().Contain("(секрет)");
    }

    [Fact]
    public async Task Valid_plain_update_returns_Ok_and_persists_value()
    {
        var (store, audit, db) = MakeFixture();

        var result = await SettingsEndpoints.UpdateAsync(
            SettingKey.IisDefaultVrdRoot,
            new UpdateSettingRequest(@"C:\inetpub\1c-publications"),
            store,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        var stored = await store.GetAsync(SettingKey.IisDefaultVrdRoot);
        stored.Should().Be(@"C:\inetpub\1c-publications");
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.SettingChanged
            && e.Description.Contains("IIS.DefaultVrdRoot"));
    }

    [Fact]
    public async Task Null_value_clears_setting()
    {
        var (store, audit, _) = MakeFixture();

        await store.SetAsync(SettingKey.OneCClusterAdminPassword, "old", isSecret: true, updatedBy: "admin");
        // Сбрасываем счётчик audit'а — нас интересует только endpoint-вызов ниже.
        audit.Entries.Clear();

        var result = await SettingsEndpoints.UpdateAsync(
            SettingKey.OneCClusterAdminPassword,
            new UpdateSettingRequest(null),
            store,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        (await store.GetAsync(SettingKey.OneCClusterAdminPassword)).Should().BeNull();
        audit.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Whitespace_value_is_treated_as_clear()
    {
        var (store, audit, _) = MakeFixture();

        var result = await SettingsEndpoints.UpdateAsync(
            SettingKey.OneCClusterAdminUser,
            new UpdateSettingRequest("   "),
            store,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        (await store.GetAsync(SettingKey.OneCClusterAdminUser)).Should().BeNull();
    }
}
