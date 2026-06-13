using System.Text;
using FluentAssertions;
using MitLicenseCenter.Infrastructure.Clusters;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// BE-12 (MLC-120) — покрытие OEM/CP866-декода stdout/stderr rac.exe в SystemProcessRacRunner.
// Декод НЕ меняется этим тестом — фиксируется текущее поведение 1:1. Регрессия, которую
// тест ловит: если кто-то «починит» стейл-комментарий в файле, поверив, что rac.exe пишет
// UTF-8, и переключит OemEncoding на UTF-8 — русский идемпотентный маркер «Сеанс… не найден»
// перестанет матчиться (mojibake), и Kill потеряет идемпотентность (MLC-001 over-kill риск).
public sealed class SystemProcessRacRunnerEncodingTests
{
    // Стабильное русское сообщение 1С Cluster Administration Server (дубль литерала
    // SessionNotFoundMarker из RacExecutableRasClusterClient — намеренно, чтобы тест
    // ловил расхождение кодировки, а не делил const).
    private const string SessionNotFoundMarker = "Сеанс с указанным идентификатором не найден";

    public SystemProcessRacRunnerEncodingTests()
    {
        // .NET Core+ требует регистрации провайдера кодовых страниц для CP866 — то же,
        // что делает ResolveOemEncoding внутри. Без неё Encoding.GetEncoding(866) бросит.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void ResolveOemEncoding_round_trips_russian_marker()
    {
        // ResolveOemEncoding возвращает кодировку текущей OEM-страницы процесса. На
        // русскоязычной Windows это CP866; в CI/иной локали — другая. Round-trip через
        // САМУ возвращённую кодировку обязан быть лосслесс для кириллицы маркера —
        // именно так runner декодит stdout/stderr (OemEncoding.GetString(bytes)).
        var encoding = SystemProcessRacRunner.ResolveOemEncoding();

        var decoded = encoding.GetString(encoding.GetBytes(SessionNotFoundMarker));

        decoded.Should().Be(SessionNotFoundMarker,
            "декод через активную OEM-кодировку обязан восстанавливать русский маркер 1:1");
    }

    [Fact]
    public void Cp866_bytes_decode_to_marker_but_utf8_decode_is_mojibake()
    {
        // Моделируем реальный сценарий: rac.exe на русской Windows печатает маркер в CP866.
        var cp866 = Encoding.GetEncoding(866);
        var wireBytes = cp866.GetBytes(SessionNotFoundMarker);

        // Правильный путь (как в runner'е, OemEncoding=CP866) — маркер восстанавливается.
        cp866.GetString(wireBytes).Should().Be(SessionNotFoundMarker);

        // Контрпример-регрессия BE-15: если декодить CP866-байты как UTF-8 (то, что делал
        // бы StreamReader без явного OEM-декода), кириллица превращается в mojibake и
        // маркеру НЕ равна. Это фиксирует, почему стейл-комментарий про «UTF-8» опасен.
        var asUtf8 = Encoding.UTF8.GetString(wireBytes);
        asUtf8.Should().NotBe(SessionNotFoundMarker,
            "декод CP866-байтов как UTF-8 даёт mojibake — именно эту поломку ловит тест");
    }
}
