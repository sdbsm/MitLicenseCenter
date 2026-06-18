using FluentAssertions;
using MitLicenseCenter.Infrastructure.Server;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Server;

// Per-service-name сериализация мутаций (ADR-55, MLC-212): один ключ — строго по
// одной операции (второй Acquire ждёт Release первого); разные ключи независимы.
public sealed class ServiceOperationGateTests
{
    [Fact]
    public async Task Same_key_serializes_second_acquire_waits_for_release()
    {
        var gate = new ServiceOperationGate();

        var first = await gate.AcquireAsync("Svc", CancellationToken.None);

        var secondTask = gate.AcquireAsync("Svc", CancellationToken.None);
        // Пока первый слот не освобождён — второй Acquire не завершается.
        secondTask.IsCompleted.Should().BeFalse();

        first.Dispose();

        var second = await secondTask; // теперь проходит
        second.Should().NotBeNull();
        second.Dispose();
    }

    [Fact]
    public async Task Different_keys_are_independent()
    {
        var gate = new ServiceOperationGate();

        var a = await gate.AcquireAsync("ServiceA", CancellationToken.None);
        // Замок другой службы не должен блокироваться захватом первой.
        var bTask = gate.AcquireAsync("ServiceB", CancellationToken.None);

        bTask.IsCompleted.Should().BeTrue();

        var b = await bTask;
        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public async Task Key_matching_is_case_insensitive()
    {
        var gate = new ServiceOperationGate();

        var first = await gate.AcquireAsync("MyService", CancellationToken.None);

        // Тот же ключ в другом регистре — та же служба, второй Acquire должен ждать.
        var secondTask = gate.AcquireAsync("myservice", CancellationToken.None);
        secondTask.IsCompleted.Should().BeFalse();

        first.Dispose();
        (await secondTask).Dispose();
    }

    [Fact]
    public async Task Double_dispose_releases_once()
    {
        var gate = new ServiceOperationGate();

        var first = await gate.AcquireAsync("Svc", CancellationToken.None);
        first.Dispose();
        first.Dispose(); // идемпотентно — не должен «вернуть» лишний слот

        // Слот свободен — берём снова и сразу.
        var again = await gate.AcquireAsync("Svc", CancellationToken.None);
        // Повторный Acquire той же службы теперь должен ждать (слот занят «again»).
        var blocked = gate.AcquireAsync("Svc", CancellationToken.None);
        blocked.IsCompleted.Should().BeFalse();

        again.Dispose();
        (await blocked).Dispose();
    }
}
