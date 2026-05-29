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

app.UseAuthentication();
app.UseAuthorization();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapAuthEndpoints(versionSet);
app.MapHealthEndpoints(versionSet);
app.MapTenantsEndpoints(versionSet);
app.MapInfobasesEndpoints(versionSet);
app.MapPublicationsEndpoints(versionSet);
app.MapAuditEndpoints(versionSet);
app.MapSessionsEndpoints(versionSet);
app.MapSettingsEndpoints(versionSet);
app.MapDashboardEndpoints(versionSet);

app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/swagger.json");
app.UseSwaggerUI(o =>
{
    o.RoutePrefix = "api/docs";
    o.SwaggerEndpoint("/api/docs/v1/swagger.json", "MitLicense Center API v1");
    o.DocumentTitle = "MitLicense Center API";
});

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

// Drift check (PR 3.5): тикаем каждые 5 мин, внутри throttle до
// Settings.Drift.IntervalMinutes — оператор может уменьшить cadence до 1 мин
// или увеличить до 60, и Hangfire-расписание сюда менять не придётся.
RecurringJob.AddOrUpdate<IDriftCheckJob>(
    "drift-check",
    j => j.RunAllAsync(CancellationToken.None),
    "*/5 * * * *");

// Audit retention (PR 4.3): ежедневно в 03:00 UTC. CRON фиксирован — retention
// window настраивается через Settings.Audit.RetentionDays, cadence — нет
// (operational noise zero, не tuneable оператором).
RecurringJob.AddOrUpdate<IAuditRetentionJob>(
    "audit-retention",
    j => j.RunAsync(CancellationToken.None),
    "0 3 * * *");

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            // IdentitySeeder применяет миграции (Migrate) до того, как создаёт
            // admin/role'ы, поэтому SettingsSeeder идёт после — таблица dbo.Settings
            // на этот момент гарантированно существует.
            await IdentitySeeder.EnsureSeededAsync(app.Services).ConfigureAwait(false);
            await SettingsSeeder.EnsureSeededAsync(app.Services).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Не удалось засеять первого администратора или параметры по умолчанию.");
            throw;
        }
    });
});

await app.RunAsync().ConfigureAwait(false);

namespace MitLicenseCenter.Web
{
    // Маркерный тип для WebApplicationFactory<Program> в тестах.
    public partial class Program;
}
