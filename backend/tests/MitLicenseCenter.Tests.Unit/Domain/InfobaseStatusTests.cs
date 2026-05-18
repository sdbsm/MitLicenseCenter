using FluentAssertions;
using MitLicenseCenter.Domain.Infobases;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Domain;

public sealed class InfobaseStatusTests
{
    // Целочисленные значения — внутренний контракт с БД (HasConversion<int>) и
    // потенциальный legacy-контракт с историческими записями. Переиспользовать или
    // переименовывать нельзя — миграция данных потребует отдельной стадии.
    [Fact]
    public void Int_values_are_frozen()
    {
        ((int)InfobaseStatus.Active).Should().Be(0);
        ((int)InfobaseStatus.Maintenance).Should().Be(1);
        ((int)InfobaseStatus.Suspended).Should().Be(2);
    }
}
