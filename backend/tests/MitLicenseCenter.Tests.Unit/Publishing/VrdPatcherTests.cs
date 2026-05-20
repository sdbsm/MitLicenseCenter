using FluentAssertions;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// Тесты pure helper'а — без файловой системы, без ServerManager.
// Покрытие: OData/HTTPServices toggle, wsisapi.dll version-патч,
// VrdCustomXml overlay, idempotency.
public sealed class VrdPatcherTests
{
    private const string MinimalVrd = """
        <?xml version="1.0" encoding="UTF-8"?>
        <point xmlns="http://v8.1c.ru/8.2/virtual-resource-system" base="/MyPub" ib="Srvr=&quot;localhost&quot;;Ref=&quot;MyBase&quot;;">
          <standardOdata enable="false" reuseSessions="autouse"/>
          <httpServices publishByDefault="false"/>
        </point>
        """;

    private static Publication Desired(
        bool odata = true,
        bool http = true,
        string version = "8.3.23.1865",
        string? customXml = null) => new()
        {
            Id = Guid.NewGuid(),
            InfobaseId = Guid.NewGuid(),
            SiteName = "Default Web Site",
            VirtualPath = "/MyPub",
            PlatformVersion = version,
            EnableOData = odata,
            EnableHttpServices = http,
            VrdCustomXml = customXml,
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public void Patch_toggles_OData_attribute_in_place()
    {
        var result = VrdPatcher.Patch(MinimalVrd, Desired(odata: true, http: false));

        VrdPatcher.TryReadODataEnabled(result, out var odata).Should().BeTrue();
        odata.Should().BeTrue();
    }

    [Fact]
    public void Patch_toggles_HttpServices_attribute_in_place()
    {
        var result = VrdPatcher.Patch(MinimalVrd, Desired(odata: false, http: true));

        VrdPatcher.TryReadHttpServicesEnabled(result, out var http).Should().BeTrue();
        http.Should().BeTrue();
    }

    [Fact]
    public void Patch_replaces_only_wsisapi_version_segment()
    {
        const string vrdWithIsapi = """
            <?xml version="1.0" encoding="UTF-8"?>
            <point xmlns="http://v8.1c.ru/8.2/virtual-resource-system" base="/MyPub" ib="Srvr=&quot;localhost&quot;;Ref=&quot;MyBase&quot;;">
              <standardOdata enable="true"/>
              <httpServices publishByDefault="true"/>
              <isapi path="C:\Program Files\1cv8\8.3.22.1709\bin\wsisapi.dll"/>
            </point>
            """;

        var result = VrdPatcher.Patch(vrdWithIsapi, Desired(version: "8.3.26.1521"));

        result.Should().Contain(@"8.3.26.1521\bin\wsisapi.dll");
        result.Should().NotContain("8.3.22.1709");
        // Префикс пути не тронут.
        result.Should().Contain(@"C:\Program Files\1cv8\");
    }

    [Fact]
    public void Patch_with_no_wsisapi_path_is_safe_noop_on_version()
    {
        // Newer 1C-сборки могут держать handler в web.config, default.vrd без
        // wsisapi.dll path. Patch не должен падать и не должен ничего изобретать.
        var result = VrdPatcher.Patch(MinimalVrd, Desired(version: "8.3.26.1521"));
        result.Should().NotContain("wsisapi.dll");
    }

    [Fact]
    public void Patch_creates_missing_OData_node_when_absent()
    {
        const string vrdWithoutOdata = """
            <?xml version="1.0" encoding="UTF-8"?>
            <point xmlns="http://v8.1c.ru/8.2/virtual-resource-system" base="/MyPub" ib="Srvr=&quot;localhost&quot;;Ref=&quot;MyBase&quot;;">
              <httpServices publishByDefault="true"/>
            </point>
            """;

        var result = VrdPatcher.Patch(vrdWithoutOdata, Desired(odata: true, http: true));

        VrdPatcher.TryReadODataEnabled(result, out var odata).Should().BeTrue();
        odata.Should().BeTrue();
    }

    [Fact]
    public void Patch_preserves_VrdCustomXml_overlay()
    {
        const string custom = """<openid url="https://idp.example.com/oauth"/>""";

        var result = VrdPatcher.Patch(MinimalVrd, Desired(customXml: custom));

        result.Should().Contain("openid");
        result.Should().Contain("https://idp.example.com/oauth");
    }

    [Fact]
    public void Patch_replaces_existing_child_when_VrdCustomXml_overrides_it()
    {
        // VrdCustomXml overrides <standardOdata> с более жёсткими настройками —
        // overlay должен заменить ноду, а не дублировать.
        const string custom = """<standardOdata enable="true" sessionMaxAge="60"/>""";

        var result = VrdPatcher.Patch(MinimalVrd, Desired(odata: false, customXml: custom));

        result.Split("<standardOdata", StringSplitOptions.None).Length.Should().Be(2,
            "overlay должен ЗАМЕНИТЬ существующий standardOdata, а не добавить второй");
        result.Should().Contain("sessionMaxAge=\"60\"");
    }

    [Fact]
    public void Patch_is_idempotent()
    {
        var d = Desired(odata: true, http: false, version: "8.3.26.1521",
            customXml: """<openid url="https://idp.example.com/oauth"/>""");

        var once = VrdPatcher.Patch(MinimalVrd, d);
        var twice = VrdPatcher.Patch(once, d);

        twice.Should().Be(once);
    }

    [Fact]
    public void TryReadPlatformVersion_extracts_version_from_wsisapi_path()
    {
        const string vrd = """
            <point xmlns="http://v8.1c.ru/8.2/virtual-resource-system">
              <isapi path="C:\Program Files\1cv8\8.3.24.1819\bin\wsisapi.dll"/>
            </point>
            """;

        VrdPatcher.TryReadPlatformVersion(vrd).Should().Be("8.3.24.1819");
    }

    [Fact]
    public void TryReadPlatformVersion_returns_null_when_no_wsisapi_path()
    {
        VrdPatcher.TryReadPlatformVersion(MinimalVrd).Should().BeNull();
    }
}
