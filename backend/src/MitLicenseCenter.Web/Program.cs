using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using MitLicenseCenter.Application;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Infrastructure;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Settings;
using MitLicenseCenter.Web;
using MitLicenseCenter.Web.Endpoints;
using MitLicenseCenter.Web.Hangfire;
using MitLicenseCenter.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// MLC-104 — корректная работа Windows-службой (установщик ADR-31 ставит exe через sc create).
// No-op в консоли/dev (детектит запуск под SCM); под службой — отвечает SCM на start/stop,
// content root = каталог exe (находит appsettings.Production.json/wwwroot), логи → Event Log.
builder.Host.UseWindowsService(o => o.ServiceName = "MitLicenseCenter");

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

// MLC-092: singleton-кэш снапшота опроса RAS для GET /infobases/unassigned
// (TTL 60 с — константа, refresh=true мимо кэша). Живёт в Web-слое, как IMemoryCache.
builder.Services.AddSingleton<UnassignedInfobasesCache>();

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

        // MLC-109 (SEC-01) — немедленный отзыв доступа. Подключаем SecurityStampValidator к
        // ревалидации куки: на каждом OnValidatePrincipal (не чаще, чем раз в
        // SecurityStampValidatorOptions.ValidationInterval = 2 мин, см. Infrastructure/DI)
        // он сверяет security-stamp из куки со stamp'ом в БД. Если админ отключил/разжаловал
        // пользователя или сбросил ему пароль — соответствующий эндпоинт ротирует stamp
        // (UpdateSecurityStampAsync), и старая кука здесь отвергается → принудительный SignOut
        // в пределах интервала. Self-смена пароля переиздаёт куку свежим stamp'ом
        // (RefreshSignInAsync) — своя сессия не рвётся.
        options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;

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
    })
    // MLC-109 (SEC-01) — обязательная пара к SecurityStampValidator. При отклонении куки
    // валидатор делает «sign-out everywhere»: SignOutAsync И по ApplicationScheme, И по
    // TwoFactorRememberMeScheme. Полный AddIdentity()/AddIdentityCookies() регистрирует обе
    // куки, но мы собираем пайплайн вручную (только ApplicationScheme) — без регистрации
    // TwoFactorRememberMe SignOutAsync по ней бросает «No sign-out handler is registered»
    // ровно в момент первого отзыва доступа. 2FA в панели не используется, поэтому это
    // чисто служебная no-op-кука, нужная лишь чтобы sign-out по схеме не падал.
    .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);

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

// MLC-125 (SEC-08) — per-IP rate limiting на /auth/login (fixed window).
// Троттлит источник перебора, не блокирует учётку (в отличие от Identity-lockout ADR-36).
// 429 возвращается ещё до тела LoginAsync — аудит LoginFailed НЕ пишется (reject до SignInManager).
// Константы вынесены для читаемости и юнит-тестирования; значения не tuneable оператором.
const string LoginRateLimitPolicy = "login";
const int LoginPermitLimit = 10;          // запросов за окно
const int LoginWindowMinutes = 1;         // длина окна (минуты)

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(LoginRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = LoginPermitLimit,
                Window = TimeSpan.FromMinutes(LoginWindowMinutes),
                QueueLimit = 0,
            }));
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
    // recurring-джобы (напр. publication-status-refresh каждые 5 мин) иначе копят историю
    // в схеме hangfire по неявному дефолту.
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

// MLC-106 (ADR-18) — ранний bootstrap: создаём целевую БД, если её ещё нет, ДО Hangfire-
// регистрации (она коннектится к БД) и до миграций/сидинга. Первопричина: EF `MigrateAsync`
// под `EnableRetryOnFailure` НЕ создаёт несуществующую БД (4060 ретраится как транзиентная →
// краш). Один сырой `CREATE DATABASE` к master обходит ловушку; `IF DB_ID IS NULL` — существующую
// БД не трогает (в dev/ops БД уже создана db-reset/инсталлятором → no-op). Гейт: только когда
// задана непустая строка подключения (InMemory-тесты через WebApplicationFactory её не задают →
// не зовём). Тот же fail-fast контракт, что и сидер: ошибка → LogCritical + throw из Main.
var defaultConnectionString = app.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(defaultConnectionString))
{
    try
    {
        await DatabaseBootstrapper.EnsureDatabaseCreatedAsync(defaultConnectionString).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Не удалось создать базу данных панели при старте.");
        throw;
    }
}

// MLC-004 — самый внешний middleware: ловит непойманные исключения из всего пайплайна
// и отдаёт их как ProblemDetails (без AddProblemDetails выше это был бы голый 500 без
// тела). В Development WebApplication заранее ставит developer-exception-page, поэтому
// разработчик по-прежнему видит подробности; в проде наружу уходит только санитизированный
// русский ProblemDetails, а полное исключение логируется этим же middleware.
app.UseExceptionHandler();

// MLC-125 (SEC-07) — security response headers на все ответы (SPA, статика, API, fallback).
// Ставится рано — до UseStaticFiles, UseAuthentication и маппинга эндпоинтов, чтобы
// заголовки попали на все ответы, включая ассеты и index.html. CSP исключается на /api/docs
// (Swagger UI использует inline-скрипты); прочие заголовки (nosniff, Referrer-Policy) —
// на всех путях. X-Frame-Options DENY дублирует CSP frame-ancestors для старых браузеров.
app.UseSecurityHeaders();

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

// MLC-098 (ADR-30) — статика SPA: Kestrel сам отдаёт собранный wwwroot same-origin с API.
// ДО аутентификации — логин-страница и её бандлы грузятся анониму. Хэшированные /assets/*
// этот middleware кэширует надолго; index.html не кэшируется (см. MapFallback ниже). В dev
// wwwroot обычно нет — middleware просто пропускает запрос дальше (страницу даёт vite :5173).
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// MLC-125 (SEC-08) — rate-limiting middleware. Ставится после UseAuthorization и ПЕРЕД
// маппингом эндпоинтов: rate-limiter не зависит от auth (пропускает до LoginAsync),
// главное — до эндпоинтов, чтобы политика "login" срабатывала на /auth/login.
app.UseRateLimiter();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapAuthEndpoints(versionSet);
app.MapHealthEndpoints(versionSet);
app.MapUsersEndpoints(versionSet);
app.MapTenantsEndpoints(versionSet);
app.MapInfobasesEndpoints(versionSet);
app.MapUnassignedInfobasesEndpoints(versionSet);
app.MapPublicationsEndpoints(versionSet);
app.MapIisEndpoints(versionSet);
app.MapAuditEndpoints(versionSet);
app.MapSessionsEndpoints(versionSet);
app.MapSettingsEndpoints(versionSet);
app.MapDashboardEndpoints(versionSet);
app.MapUpdatesEndpoints(versionSet);
app.MapReportsEndpoints(versionSet);
app.MapPerformanceEndpoints(versionSet);
app.MapBackupsEndpoints(versionSet);
app.MapDiscoveryEndpoints(versionSet);
app.MapRasServiceEndpoints(versionSet);

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

// MLC-098 (ADR-30) — SPA history-fallback: запрос, не подхваченный ни одним эндпоинтом/статикой,
// отдаёт оболочку index.html, чтобы deep-link/refresh клиентских маршрутов (createBrowserRouter,
// HTML5-history) работали. Анонимный — оболочку грузит и неаутентифицированный (логин рисует SPA).
// Зарезервированные /api и /hangfire НЕ перехватываем: неизвестный /api/* честно отдаёт 404,
// а не маскируется под HTML. В dev без wwwroot fallback тоже отдаёт 404 (страницу даёт vite :5173).
app.MapFallback(async context =>
{
    if (SpaFallback.IsReservedPath(context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var index = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
    if (app.Environment.WebRootPath is null || !File.Exists(index))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    // index.html НЕ кэшируем (ссылается на хэшированные /assets/*); сами ассеты кэширует UseStaticFiles.
    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(index).ConfigureAwait(false);
}).AllowAnonymous();

// Graceful shutdown (MLC-123, BE-20): CancellationToken.None в выражениях ниже — это
// ИДИОМАТИЧНЫЙ ПЛЕЙСХОЛДЕР Hangfire, а НЕ «вечно-неотменяемый» токен. При выполнении
// job'ы Hangfire подменяет его реальным токеном, сигналящимся при остановке сервера и
// при abort'е задачи (см. JobCancellationToken / IJobCancellationToken). Тела всех джоб
// прокидывают ct в свои EF/IO-вызовы (ct.ThrowIfCancellationRequested / передача в
// ExecuteSql/SaveChanges), поэтому при остановке службы они завершаются кооперативно.
// НЕ заменять CancellationToken.None на захваченный токен — это сломает подстановку и
// джоба получит токен, который никогда не сигналит при shutdown. Контракт «тело
// уважает ct» зафиксирован характеризующим тестом (JobCancellationContractTests).
//
// MLC-125: пропускаем регистрацию рекуррентных джоб в тест-среде (WebApplicationFactory
// использует "Test" environment). Производственный Hangfire Storage (SqlServer) не
// доступен при интеграционных тестах — RecurringJob.AddOrUpdate использует JobStorage.Current
// (статический синглтон), который в тест-среде заменить на InMemory через ConfigureTestServices
// технически невозможно до этой точки выполнения Program.cs.
if (!app.Environment.IsEnvironment("Test"))
{
    // MLC-154: cold-цикл согласования больше НЕ Hangfire-recurring — он перенесён в
    // ColdTierPollingService (BackgroundService). Hangfire-CRON minimum = 1 мин делал
    // настройку Polling.ColdIntervalSeconds инертной; таймер сервиса соблюдает её реально.
    // Стартовый warm-up снимка теперь — немедленный первый прогон в ExecuteAsync сервиса.
    RecurringJob.RemoveIfExists("cold-snapshot");

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

    // Perf recording retention (MLC-169): ежедневно в 03:45 UTC — смещён от 03:00 audit,
    // 03:15 backup и 03:30 license-usage, чтобы ночные housekeeping-джобы не пересекались.
    // Срок хранения зашит константой в джобе (не настраивается оператором); cadence фиксирован.
    RecurringJob.AddOrUpdate<IPerfRecordingRetentionJob>(
        "perf-recording-retention",
        j => j.RunAsync(CancellationToken.None),
        "45 3 * * *");
} // end if (!app.Environment.IsEnvironment("Test"))

// Fail-fast bootstrap. Миграции и сидинг выполняются СИНХРОННО до открытия приёма
// трафика (до app.RunAsync()), каждый в собственном DI-scope внутри сидера. Порядок
// сохранён: миграции + admin/role'ы (IdentitySeeder) → SettingsSeeder (таблица
// dbo.Settings к этому моменту уже создана миграцией). В Development стартовый пароль
// admin'а по-прежнему пишется в лог самим IdentitySeeder. При любой ошибке инициализации
// логируем LogCritical и пробрасываем исключение из Main — процесс падает с ненулевым
// кодом и НИКОГДА не начинает принимать запросы «полузасеянным» (без admin'а или с
// неприменёнными миграциями). Это устраняет прежний fire-and-forget Task.Run, в котором
// throw оставался unobserved и хост тихо стартовал в нерабочем состоянии.
// MLC-125: в тест-среде ("Test") сидеры тоже пропускаются — IdentitySeeder вызывает
// EF MigrateAsync (требует SQL Server); InMemory DbContext мигрировать нельзя.
// Тест-хост поднимается без admin'а — нам нужны только middleware-пайплайн и rate-limiter.
if (!app.Environment.IsEnvironment("Test"))
{
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
}

await app.RunAsync().ConfigureAwait(false);

namespace MitLicenseCenter.Web
{
    // Маркерный тип для WebApplicationFactory<Program> в тестах.
    public partial class Program;
}
