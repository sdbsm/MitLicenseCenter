using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using MitLicenseCenter.Application;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Infrastructure;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Settings;
using MitLicenseCenter.Web;
using MitLicenseCenter.Web.Endpoints;
using MitLicenseCenter.Web.Hangfire;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // Enum'ы домена (AuditActionType, AuditReason и др.) сериализуются по имени —
    // машинно-читаемый контракт для frontend, без хрупкой привязки к числам.
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// MLC-004 — глобальный RFC7807 ProblemDetails. Непойманные исключения (и любой иной
// ненастроенный путь ошибки) отдаются как ProblemDetails, а не как голый 500 без тела.
// Детали наружу — русские и санитизированные: stack trace / текст исключения НИКОГДА
// не попадают в ответ (реальное исключение пишет в лог сам UseExceptionHandler).
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
        ctx.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;

        // 5xx (в т.ч. непойманные исключения): подменяем дефолтный англоязычный текст
        // на нейтральное русское сообщение без внутренних деталей.
        if (ctx.ProblemDetails.Status is >= 500)
        {
            ctx.ProblemDetails.Title = "Внутренняя ошибка сервера";
            ctx.ProblemDetails.Detail =
                "Произошла непредвиденная ошибка. Повторите попытку позже или обратитесь к администратору.";
        }
    };
});

// IMemoryCache: используется DashboardEndpoints (TTL=5s) и потенциально другими
// hot-path ридерами. AddInfrastructure его не регистрирует — кэш живёт в Web-слое.
builder.Services.AddMemoryCache();

// Cookie auth tuned for an SPA (401 JSON, not 302 redirect).
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "mlc.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Roles.Viewer, p => p.RequireRole(Roles.Admin, Roles.Viewer));

builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
        o.ReportApiVersions = true;
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MitLicense Center API",
        Version = "v1",
        Description = "Панель управления 1С-хостингом.",
    });
});

// Hangfire — dashboard only on Stage 1. Recurring jobs land in Stage 2+.
var hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Не задана строка подключения для Hangfire ('ConnectionStrings:Hangfire' или 'ConnectionStrings:Default').");

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    // Детерминированный срок хранения завершённых джоб (см. JobRetentionStateFilter):
    // cold-snapshot раз в минуту иначе копит историю в схеме hangfire по неявному дефолту.
    .UseFilter(new JobRetentionStateFilter())
    .UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions
    {
        SchemaName = "hangfire",
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15),
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        PrepareSchemaIfNecessary = true,
    }));

builder.Services.AddHangfireServer(o =>
{
    o.WorkerCount = Math.Max(2, Environment.ProcessorCount);
});

// HotTierPollingService + RasHealthProbingService registered in
// Infrastructure.DependencyInjection.

var app = builder.Build();

// MLC-004 — самый внешний middleware: ловит непойманные исключения из всего пайплайна
// и отдаёт их как ProblemDetails (без AddProblemDetails выше это был бы голый 500 без
// тела). В Development WebApplication заранее ставит developer-exception-page, поэтому
// разработчик по-прежнему видит подробности; в проде наружу уходит только санитизированный
// русский ProblemDetails, а полное исключение логируется этим же middleware.
app.UseExceptionHandler();

// MLC-012 — прод-хардненинг транспорта (HSTS + HTTPS-redirect) за флагом, НЕ безусловно.
// Single-node может стоять либо за терминирующим TLS реверс-прокси (IIS/Nginx), либо без
// него с HTTPS прямо на Kestrel. Включать redirect/HSTS в приложении нужно ТОЛЬКО во
// втором случае — иначе за прокси получаются двойной редирект / петли (приложению на
// localhost приходит уже расшифрованный http). Поэтому:
//   • выключено в Development (dev ходит по http к локальному SQL без шифрования);
//   • вне Development управляется флагом Security:EnforceHttps (по умолчанию false).
// Оператор ставит true ТОЛЬКО когда сервис сам терминирует TLS (нет прокси + есть
// сертификат на Kestrel); за терминирующим прокси флаг остаётся false — redirect/HSTS
// делает прокси. См. OPERATIONS.md «Transport hardening». Cookie Secure=Always в проде
// уже задаётся независимо от этого флага (см. AddCookie выше).
var enforceHttps = TransportSecurity.ShouldEnforceHttps(
    app.Environment.IsDevelopment(), app.Configuration);
if (enforceHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapAuthEndpoints(versionSet);
app.MapHealthEndpoints(versionSet);
app.MapUsersEndpoints(versionSet);
app.MapTenantsEndpoints(versionSet);
app.MapInfobasesEndpoints(versionSet);
app.MapPublicationsEndpoints(versionSet);
app.MapIisEndpoints(versionSet);
app.MapAuditEndpoints(versionSet);
app.MapSessionsEndpoints(versionSet);
app.MapSettingsEndpoints(versionSet);
app.MapDashboardEndpoints(versionSet);
app.MapReportsEndpoints(versionSet);
app.MapPerformanceEndpoints(versionSet);
app.MapBackupsEndpoints(versionSet);
app.MapDiscoveryEndpoints(versionSet);

// MLC-012 — Swagger UI (/api/docs) раскрывает всю карту API. В Development отдаётся
// всегда (на нём держится ручная синхронизация TS-типов — ADR-10.1). В проде закрыт
// по умолчанию; override-флаг Security:EnableSwagger=true возвращает его для отладки на
// внутреннем admin-only периметре. См. OPERATIONS.md «Transport hardening».
var enableSwagger = TransportSecurity.ShouldEnableSwagger(
    app.Environment.IsDevelopment(), app.Configuration);
if (enableSwagger)
{
    app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/swagger.json");
    app.UseSwaggerUI(o =>
    {
        o.RoutePrefix = "api/docs";
        o.SwaggerEndpoint("/api/docs/v1/swagger.json", "MitLicense Center API v1");
        o.DocumentTitle = "MitLicense Center API";
    });
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new AdminOnlyDashboardAuthorizationFilter()],
    DashboardTitle = "MitLicense Center · Hangfire",
    DisplayStorageConnectionString = false,
});

RecurringJob.AddOrUpdate<IReconciliationJob>(
    "cold-snapshot",
    j => j.RunColdAsync(CancellationToken.None),
    "* * * * *"); // Every minute; internal throttle enforces ColdIntervalSeconds.

// Publication status refresh (MLC-045): тикаем каждые 5 мин, внутри throttle до
// Settings.Drift.IntervalMinutes. Read-only — читает факт публикаций в IIS и пишет
// LastCheck*; ничего не меняет и не пишет аудит (enforcement удалён).
RecurringJob.AddOrUpdate<IPublicationStatusJob>(
    "publication-status-refresh",
    j => j.RefreshAllAsync(CancellationToken.None),
    "*/5 * * * *");
RecurringJob.RemoveIfExists("drift-check");

// Audit retention (PR 4.3): ежедневно в 03:00 UTC. CRON фиксирован — retention
// window настраивается через Settings.Audit.RetentionDays, cadence — нет
// (operational noise zero, не tuneable оператором).
RecurringJob.AddOrUpdate<IAuditRetentionJob>(
    "audit-retention",
    j => j.RunAsync(CancellationToken.None),
    "0 3 * * *");

// License usage retention (MLC-048): ежедневно в 03:30 UTC, смещён от audit-retention
// (03:00), чтобы ночные housekeeping-джобы не пересекались. Retention window —
// Settings.LicenseUsage.RetentionDays; cadence фиксирован.
RecurringJob.AddOrUpdate<ILicenseUsageRetentionJob>(
    "license-usage-retention",
    j => j.RunAsync(CancellationToken.None),
    "30 3 * * *");

// Backup retention (MLC-077, ADR-27): ежедневно в 03:15 UTC — смещён от 03:00
// audit-retention и 03:30 license-usage, чтобы ночные housekeeping-джобы не
// пересекались. TTL — Settings.Backup.TtlHours; cadence фиксирован.
RecurringJob.AddOrUpdate<IBackupRetentionJob>(
    "backup-retention",
    j => j.RunAsync(CancellationToken.None),
    "15 3 * * *");

// Fail-fast bootstrap. Миграции и сидинг выполняются СИНХРОННО до открытия приёма
// трафика (до app.RunAsync()), каждый в собственном DI-scope внутри сидера. Порядок
// сохранён: миграции + admin/role'ы (IdentitySeeder) → SettingsSeeder (таблица
// dbo.Settings к этому моменту уже создана миграцией). В Development стартовый пароль
// admin'а по-прежнему пишется в лог самим IdentitySeeder. При любой ошибке инициализации
// логируем LogCritical и пробрасываем исключение из Main — процесс падает с ненулевым
// кодом и НИКОГДА не начинает принимать запросы «полузасеянным» (без admin'а или с
// неприменёнными миграциями). Это устраняет прежний fire-and-forget Task.Run, в котором
// throw оставался unobserved и хост тихо стартовал в нерабочем состоянии.
try
{
    await IdentitySeeder.EnsureSeededAsync(app.Services).ConfigureAwait(false);
    await SettingsSeeder.EnsureSeededAsync(app.Services).ConfigureAwait(false);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Не удалось засеять первого администратора или параметры по умолчанию.");
    throw;
}

await app.RunAsync().ConfigureAwait(false);

namespace MitLicenseCenter.Web
{
    // Маркерный тип для WebApplicationFactory<Program> в тестах.
    public partial class Program;
}
