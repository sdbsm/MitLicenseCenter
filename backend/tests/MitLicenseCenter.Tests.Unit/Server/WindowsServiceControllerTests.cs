using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Infrastructure.Ras;
using MitLicenseCenter.Infrastructure.Server;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Server;

// Контракт надёжности ADR-55 (MLC-212): команда (sc start/sc stop) + ВЕРИФИКАЦИЯ
// фактического состояния опросом ServiceController до целевого с таймаутом;
// идемпотентность (sc 1056/1062 = успех); сериализация мутаций одной службы.
// Реальные sc.exe/ServiceController не дёргаются — фейки FakeScRunner/FakeState
// (адаптированы из ScRasServiceManagerTests; FakeState умеет менять состояние между
// опросами). Таймауты-опции мизерные → тест таймаута мгновенный.
public sealed class WindowsServiceControllerTests
{
    private const string Svc = "MyService";

    // Success / verification / идемпотентность / restart:
    // VerificationTimeout огромный — дедлайн НИКОГДА ложно не истекает под нагрузкой.
    // PollInterval=Zero — опросы прокручиваются мгновенно (Task.Delay(Zero) = yield).
    // FakeState переключается по числу чтений, поэтому тест завершится успехом сразу,
    // не зависая от настенных часов и параллельной нагрузки сборщика.
    private static readonly WindowsServiceControllerOptions ReliableOptions = new()
    {
        PollInterval = TimeSpan.Zero,
        VerificationTimeout = TimeSpan.FromSeconds(30),
    };

    // Timeout-тесты (служба никогда не достигает цели):
    // VerificationTimeout крошечный — дедлайн детерминированно истекает почти мгновенно.
    // Нагрузка лишь ускоряет истечение — направление корректное, флак невозможен.
    private static readonly WindowsServiceControllerOptions TimeoutOptions = new()
    {
        PollInterval = TimeSpan.Zero,
        VerificationTimeout = TimeSpan.FromMilliseconds(1),
    };

    // ── Верификация ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_succeeds_after_polling_until_running()
    {
        // sc=0; служба становится Running лишь на 3-м опросе → проверяем именно полинг.
        var sc = new FakeScRunner();
        var state = new FakeState(initialRunning: false).RunningAfterReads(3);

        var result = await NewController(sc, state).StartAsync(Svc, CancellationToken.None);

        result.ServiceName.Should().Be(Svc);
        result.FinalStatus.Should().Be(WindowsServiceStatus.Running);
        sc.Calls.Should().ContainSingle().Which.Should().Be("start \"MyService\"");
    }

    // Регресс MLC-223: имя службы 1С содержит пробелы/двоеточие/скобки
    // (`1C:Enterprise 8.5 Server Agent (x86-64)`). Без кавычек sc.exe видит мусорную
    // командную строку → 1639 ERROR_INVALID_COMMAND_LINE. Имя ОБЯЗАНО квотироваться.
    [Fact]
    public async Task Stop_quotes_service_name_with_spaces()
    {
        const string oneCName = "1C:Enterprise 8.5 Server Agent (x86-64)";
        var sc = new FakeScRunner();
        var state = new FakeState(initialRunning: true).StoppedAfterReads(2);

        await NewController(sc, state).StopAsync(oneCName, CancellationToken.None);

        sc.Calls.Should().ContainSingle().Which.Should().Be($"stop \"{oneCName}\"");
    }

    [Fact]
    public async Task Stop_succeeds_after_polling_until_stopped()
    {
        var sc = new FakeScRunner();
        var state = new FakeState(initialRunning: true).StoppedAfterReads(3);

        var result = await NewController(sc, state).StopAsync(Svc, CancellationToken.None);

        result.FinalStatus.Should().Be(WindowsServiceStatus.Stopped);
        sc.Calls.Should().ContainSingle().Which.Should().Be("stop \"MyService\"");
    }

    // ── Таймаут ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_throws_when_service_never_reaches_running()
    {
        var sc = new FakeScRunner();
        var state = new FakeState(initialRunning: false); // никогда не Running

        // TimeoutOptions: бюджет 1мс — дедлайн детерминированно истекает почти мгновенно.
        var act = () => NewController(sc, state, TimeoutOptions).StartAsync(Svc, CancellationToken.None);

        await act.Should().ThrowAsync<WindowsServiceOperationException>();
    }

    [Fact]
    public async Task Stop_throws_when_service_never_reaches_stopped()
    {
        var sc = new FakeScRunner();
        var state = new FakeState(initialRunning: true); // никогда не Stopped

        // TimeoutOptions: бюджет 1мс — дедлайн детерминированно истекает почти мгновенно.
        var act = () => NewController(sc, state, TimeoutOptions).StopAsync(Svc, CancellationToken.None);

        await act.Should().ThrowAsync<WindowsServiceOperationException>();
    }

    // ── Идемпотентность ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_treats_already_running_sc_1056_as_success()
    {
        var sc = new FakeScRunner { ExitCode = 1056 }; // ERROR_SERVICE_ALREADY_RUNNING
        var state = new FakeState(initialRunning: true); // уже работает

        var result = await NewController(sc, state).StartAsync(Svc, CancellationToken.None);

        result.FinalStatus.Should().Be(WindowsServiceStatus.Running);
    }

    [Fact]
    public async Task Stop_treats_not_active_sc_1062_as_success()
    {
        var sc = new FakeScRunner { ExitCode = 1062 }; // ERROR_SERVICE_NOT_ACTIVE
        var state = new FakeState(initialRunning: false); // уже остановлена

        var result = await NewController(sc, state).StopAsync(Svc, CancellationToken.None);

        result.FinalStatus.Should().Be(WindowsServiceStatus.Stopped);
    }

    // ── Жёсткий сбой sc ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_throws_on_hard_sc_failure_access_denied()
    {
        var sc = new FakeScRunner { ExitCode = 5 }; // ERROR_ACCESS_DENIED
        var state = new FakeState(initialRunning: false).RunningAfterReads(1);

        var act = () => NewController(sc, state).StartAsync(Svc, CancellationToken.None);

        await act.Should().ThrowAsync<WindowsServiceOperationException>();
    }

    // ── Служба не найдена ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_throws_when_service_not_found_during_polling()
    {
        var sc = new FakeScRunner(); // sc=0
        var state = new FakeState(); // ReadState=null — службы нет

        var act = () => NewController(sc, state).StartAsync(Svc, CancellationToken.None);

        await act.Should().ThrowAsync<WindowsServiceOperationException>();
    }

    // ── Restart ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Restart_runs_stop_then_start_both_verified()
    {
        var sc = new FakeScRunner();
        // Сначала Running → стоп уводит в Stopped, затем старт возвращает в Running.
        var state = new FakeState(initialRunning: true)
            .StoppedAfterReads(1)
            .ThenRunningAfterReads(1);

        var result = await NewController(sc, state).RestartAsync(Svc, CancellationToken.None);

        result.FinalStatus.Should().Be(WindowsServiceStatus.Running);
        sc.Calls.Should().ContainInOrder("stop \"MyService\"", "start \"MyService\"");
    }

    // Регресс MLC-225: переходное StopPending (IsRunning=false И IsStopped=false) НЕ считается
    // остановкой. Иначе в рестарте `sc start` уходил бы в ещё останавливающуюся службу и не
    // действовал (симптом: «служба останавливается, но не запускается»). Стоп на вечном pending
    // обязан истечь по таймауту, а не ложно «успешно остановиться».
    [Fact]
    public async Task Stop_does_not_treat_pending_as_stopped()
    {
        var sc = new FakeScRunner();
        var state = new FakeState(initialRunning: true).Pending(); // всегда StopPending, никогда Stopped

        var act = () => NewController(sc, state, TimeoutOptions).StopAsync(Svc, CancellationToken.None);

        await act.Should().ThrowAsync<WindowsServiceOperationException>();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────

    // Без явного аргумента options — ReliableOptions (success/verification/restart/идемпотентность).
    // Для timeout-тестов передавать TimeoutOptions явно.
    private static WindowsServiceController NewController(
        FakeScRunner sc,
        FakeState state,
        WindowsServiceControllerOptions? options = null) =>
        new(
            sc,
            state,
            new ServiceOperationGate(),
            TimeProvider.System,
            options ?? ReliableOptions,
            NullLogger<WindowsServiceController>.Instance);

    // Фейк sc.exe: возвращает заданный ExitCode на любую команду, фиксирует ПОЛНУЮ строку
    // аргументов как есть (важно для регресса MLC-223: имя службы 1С с пробелами обязано
    // приходить в кавычках — `stop "1C:Enterprise 8.5 Server Agent (x86-64)"`; разбор по
    // пробелу мангал бы такие имена и прятал баг).
    private sealed class FakeScRunner : IScProcessRunner
    {
        public List<string> Calls { get; } = new();
        public int ExitCode { get; set; }

        public Task<ScResult> RunAsync(string arguments, CancellationToken ct)
        {
            Calls.Add(arguments);
            return Task.FromResult(new ScResult(ExitCode, "", ""));
        }
    }

    // Фейк ServiceController с управляемой сменой состояния между опросами: сценарий
    // «переключиться в target после N чтений», с возможностью набрать второй переход
    // (для Restart: Stopped после стопа, затем Running после старта). Без записей —
    // ReadState=null (службы нет).
    private sealed class FakeState : IServiceStateReader
    {
        private readonly bool _hasService;
        private bool _current;
        private readonly Queue<(int AfterReads, bool Target)> _transitions = new();
        private int _readsSinceTransition;
        private bool _pending;

        public FakeState()
        {
            _hasService = false;
        }

        public FakeState(bool initialRunning)
        {
            _hasService = true;
            _current = initialRunning;
        }

        public FakeState RunningAfterReads(int reads) => Schedule(reads, true);

        public FakeState StoppedAfterReads(int reads) => Schedule(reads, false);

        public FakeState ThenRunningAfterReads(int reads) => Schedule(reads, true);

        // Зафиксировать переходное состояние StopPending (IsRunning=false И IsStopped=false) —
        // регресс MLC-225: «пендинг ≠ остановлена».
        public FakeState Pending()
        {
            _pending = true;
            return this;
        }

        private FakeState Schedule(int reads, bool target)
        {
            _transitions.Enqueue((reads, target));
            return this;
        }

        public ServiceState? ReadState(string serviceName)
        {
            if (!_hasService)
            {
                return null;
            }

            if (_transitions.Count > 0)
            {
                var next = _transitions.Peek();
                if (_readsSinceTransition >= next.AfterReads)
                {
                    _current = next.Target;
                    _transitions.Dequeue();
                    _readsSinceTransition = 0;
                }
                else
                {
                    _readsSinceTransition++;
                }
            }

            // _pending → переходное StopPending: ни Running, ни Stopped (оба false).
            // Иначе бинарно: IsStopped = !IsRunning (фейк без отдельного StartPending).
            return _pending
                ? new ServiceState(false, false, serviceName)
                : new ServiceState(_current, !_current, serviceName);
        }
    }
}
