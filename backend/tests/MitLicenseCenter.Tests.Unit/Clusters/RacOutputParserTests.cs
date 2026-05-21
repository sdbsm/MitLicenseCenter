using FluentAssertions;
using MitLicenseCenter.Infrastructure.Clusters;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Fixtures сняты с реального rac.exe v8.5.1.1302 в plan-mode фазе PR 3.8.
// См. docs/DECISIONS.md ADR-3.3.
public sealed class RacOutputParserTests
{
    [Fact]
    public void Empty_string_returns_empty_list()
    {
        var result = RacOutputParser.Parse(string.Empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Null_input_returns_empty_list()
    {
        var result = RacOutputParser.Parse(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Whitespace_only_input_returns_empty_list()
    {
        var result = RacOutputParser.Parse("   \r\n\r\n\t\r\n");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parses_single_cluster_record_from_real_rac_output()
    {
        // Captured: rac.exe cluster list (1C 8.5.1.1302, local test rig).
        const string stdout =
            "cluster                                   : 613f185a-339d-4bc5-88ad-16acd14a4d26\r\n" +
            "host                                      : Andrey-pc\r\n" +
            "port                                      : 1541\r\n" +
            "name                                      : \"Локальный кластер\"\r\n" +
            "expiration-timeout                        : 60\r\n" +
            "lifetime-limit                            : 0\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(1);
        var rec = records[0];
        rec["cluster"].Should().Be("613f185a-339d-4bc5-88ad-16acd14a4d26");
        rec["host"].Should().Be("Andrey-pc");
        rec["port"].Should().Be("1541");
        rec["name"].Should().Be("Локальный кластер", "кавычки вокруг строкового значения снимаются");
        rec["expiration-timeout"].Should().Be("60");
        rec["lifetime-limit"].Should().Be("0");
    }

    [Fact]
    public void Parses_real_session_list_record_with_1cv8c_app_id()
    {
        // Captured: rac.exe session list --cluster=<uuid> для активной 1CV8C-сессии.
        const string stdout =
            "session                          : 492af167-20e6-497a-9eef-20ce4e930c6a\r\n" +
            "session-id                       : 1\r\n" +
            "infobase                         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "connection                       : 00000000-0000-0000-0000-000000000000\r\n" +
            "process                          : 00000000-0000-0000-0000-000000000000\r\n" +
            "user-name                        : Андрей\r\n" +
            "host                             : \r\n" +
            "app-id                           : 1CV8C\r\n" +
            "locale                           : ru_RU\r\n" +
            "started-at                       : 2026-05-21T03:39:49\r\n" +
            "last-active-at                   : 2026-05-21T03:50:05\r\n" +
            "hibernate                        : no\r\n" +
            "data-separation                  : ''\r\n" +
            "client-ip                        : ::1\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(1);
        var rec = records[0];
        rec["session"].Should().Be("492af167-20e6-497a-9eef-20ce4e930c6a");
        rec["infobase"].Should().Be("6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa");
        rec["app-id"].Should().Be("1CV8C");
        rec["user-name"].Should().Be("Андрей", "кириллица сохраняется (UTF-8 без BOM)");
        rec["host"].Should().Be(string.Empty, "пустое значение после `: ` парсится как пустая строка");
        rec["started-at"].Should().Be("2026-05-21T03:39:49");
        rec["client-ip"].Should().Be("::1");
    }

    [Fact]
    public void Parses_two_session_records_separated_by_blank_line()
    {
        const string stdout =
            "session                          : 11111111-1111-1111-1111-111111111111\r\n" +
            "infobase                         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id                           : 1CV8C\r\n" +
            "user-name                        : Andrey\r\n" +
            "started-at                       : 2026-05-21T03:39:49\r\n" +
            "\r\n" +
            "session                          : 22222222-2222-2222-2222-222222222222\r\n" +
            "infobase                         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id                           : BackgroundJob\r\n" +
            "user-name                        : DefUser\r\n" +
            "started-at                       : 2026-05-21T03:38:35\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(2, "order preservation важна для kill-priority newest-first");
        records[0]["session"].Should().Be("11111111-1111-1111-1111-111111111111");
        records[0]["app-id"].Should().Be("1CV8C");
        records[1]["session"].Should().Be("22222222-2222-2222-2222-222222222222");
        records[1]["app-id"].Should().Be("BackgroundJob");
    }

    [Fact]
    public void Multiple_blank_lines_treated_as_single_record_separator()
    {
        const string stdout =
            "cluster : aaaa-bbbb\r\n" +
            "\r\n" +
            "\r\n" +
            "\r\n" +
            "cluster : cccc-dddd\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(2, "несколько пустых строк подряд = один разделитель");
        records[0]["cluster"].Should().Be("aaaa-bbbb");
        records[1]["cluster"].Should().Be("cccc-dddd");
    }

    [Fact]
    public void Malformed_lines_skipped_without_throwing()
    {
        const string stdout =
            "session : 11111111-1111-1111-1111-111111111111\r\n" +
            "this is not a key-value pair\r\n" +
            "###%%%nonsense\r\n" +
            "infobase : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(1);
        records[0].Should().ContainKey("session").And.ContainKey("infobase");
        records[0].Should().HaveCount(2, "malformed строки выкинуты, валидные остались");
    }

    [Fact]
    public void Unknown_keys_passed_through_unchanged()
    {
        // Адаптер сам отфильтровывает интересующие поля; парсер должен
        // вернуть всё, что распарсилось.
        const string stdout =
            "session : 11111111-1111-1111-1111-111111111111\r\n" +
            "infobase : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "future-rac-field-we-dont-know : whatever\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(1);
        records[0]["future-rac-field-we-dont-know"].Should().Be("whatever");
    }

    [Fact]
    public void Trailing_record_without_final_blank_line_still_emitted()
    {
        // Реальный rac.exe иногда не выдаёт хвостовую пустую строку.
        const string stdout =
            "cluster : aaaa-bbbb\r\n" +
            "name    : First\r\n" +
            "\r\n" +
            "cluster : cccc-dddd\r\n" +
            "name    : Last";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(2);
        records[1]["cluster"].Should().Be("cccc-dddd");
        records[1]["name"].Should().Be("Last");
    }

    [Fact]
    public void Both_lf_and_crlf_line_endings_supported()
    {
        // Если откуда-то прилетит LF-only (например, текстовый файл/тест-фикстура).
        const string stdout =
            "cluster : aaaa-bbbb\n" +
            "name    : LfOnly\n" +
            "\n" +
            "cluster : cccc-dddd\n" +
            "name    : Mixed\r\n";

        var records = RacOutputParser.Parse(stdout);

        records.Should().HaveCount(2);
        records[0]["name"].Should().Be("LfOnly");
        records[1]["name"].Should().Be("Mixed");
    }
}
