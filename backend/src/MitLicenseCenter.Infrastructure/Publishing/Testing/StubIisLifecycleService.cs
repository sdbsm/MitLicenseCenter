using MitLicenseCenter.Application.Publishing;

namespace MitLicenseCenter.Infrastructure.Publishing.Testing;

// Заглушка для unit-тестов (MLC-047), которым не нужен реальный ServerManager/iisreset.
// В production-DI не регистрируется — реальный OneCIisLifecycleService требует Windows.
// Настраивается через публичные поля; считает вызовы для проверки в тестах.
internal sealed class StubIisLifecycleService : IIisLifecycleService
{
    public List<IisAppPoolInfo> Pools { get; } = [];
    public List<IisSiteStateInfo> Sites { get; } = [];

    // Если задано — соответствующая мутация бросает это исключение (тест 404/409-путей).
    public Exception? PoolOperationThrows { get; set; }
    public Exception? SiteOperationThrows { get; set; }
    public Exception? ServerOperationThrows { get; set; }

    // Состояние, которое вернёт мутация пула/сайта при успехе.
    public IisObjectState ResultState { get; set; } = IisObjectState.Started;

    // Состояние IIS в целом (W3SVC), которое вернёт GetServerStateAsync.
    public IisObjectState ServerState { get; set; } = IisObjectState.Started;
    public Exception? ServerStateThrows { get; set; }

    public int RecyclePoolCalls { get; private set; }
    public int StartPoolCalls { get; private set; }
    public int StopPoolCalls { get; private set; }
    public int StartSiteCalls { get; private set; }
    public int StopSiteCalls { get; private set; }
    public int RestartSiteCalls { get; private set; }
    public int RestartIisCalls { get; private set; }
    public int StopIisCalls { get; private set; }
    public int StartIisCalls { get; private set; }
    public string? LastPoolName { get; private set; }
    public string? LastSiteName { get; private set; }

    public Task<IReadOnlyList<IisAppPoolInfo>> ListApplicationPoolsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IisAppPoolInfo>>(Pools);

    public Task<IReadOnlyList<IisSiteStateInfo>> ListSitesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IisSiteStateInfo>>(Sites);

    public Task<IisObjectState> GetServerStateAsync(CancellationToken ct)
    {
        if (ServerStateThrows is not null) throw ServerStateThrows;
        return Task.FromResult(ServerState);
    }

    public Task<IisObjectState> RecycleApplicationPoolAsync(string poolName, CancellationToken ct)
    {
        RecyclePoolCalls++;
        LastPoolName = poolName;
        if (PoolOperationThrows is not null) throw PoolOperationThrows;
        return Task.FromResult(ResultState);
    }

    public Task<IisObjectState> StartApplicationPoolAsync(string poolName, CancellationToken ct)
    {
        StartPoolCalls++;
        LastPoolName = poolName;
        if (PoolOperationThrows is not null) throw PoolOperationThrows;
        return Task.FromResult(ResultState);
    }

    public Task<IisObjectState> StopApplicationPoolAsync(string poolName, CancellationToken ct)
    {
        StopPoolCalls++;
        LastPoolName = poolName;
        if (PoolOperationThrows is not null) throw PoolOperationThrows;
        return Task.FromResult(ResultState);
    }

    public Task<IisObjectState> StartSiteAsync(string siteName, CancellationToken ct)
    {
        StartSiteCalls++;
        LastSiteName = siteName;
        if (SiteOperationThrows is not null) throw SiteOperationThrows;
        return Task.FromResult(ResultState);
    }

    public Task<IisObjectState> StopSiteAsync(string siteName, CancellationToken ct)
    {
        StopSiteCalls++;
        LastSiteName = siteName;
        if (SiteOperationThrows is not null) throw SiteOperationThrows;
        return Task.FromResult(ResultState);
    }

    public Task<IisObjectState> RestartSiteAsync(string siteName, CancellationToken ct)
    {
        RestartSiteCalls++;
        LastSiteName = siteName;
        if (SiteOperationThrows is not null) throw SiteOperationThrows;
        return Task.FromResult(ResultState);
    }

    public Task RestartIisAsync(CancellationToken ct)
    {
        RestartIisCalls++;
        if (ServerOperationThrows is not null) throw ServerOperationThrows;
        return Task.CompletedTask;
    }

    public Task StopIisAsync(CancellationToken ct)
    {
        StopIisCalls++;
        if (ServerOperationThrows is not null) throw ServerOperationThrows;
        return Task.CompletedTask;
    }

    public Task StartIisAsync(CancellationToken ct)
    {
        StartIisCalls++;
        if (ServerOperationThrows is not null) throw ServerOperationThrows;
        return Task.CompletedTask;
    }
}
