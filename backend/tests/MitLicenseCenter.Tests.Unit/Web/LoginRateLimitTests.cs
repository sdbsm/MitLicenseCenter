using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Web;

// MLC-125 (SEC-08) — интеграционные тесты rate-limiting на POST /api/v1/auth/login.
// Проверяем, что при флуде с одного IP после PermitLimit=10 запросов в течение 1 минуты
// возвращается 429 Too Many Requests.
//
// Каждый тест-класс использует собственный MlcWebApplicationFactory (IClassFixture),
// обеспечивая изолированный rate-limiter state. Тест-классы намеренно разделены, чтобы
// state флуд-теста не влиял на тест штатного запроса.
[Collection("WebApp")]
public sealed class LoginRateLimitFloodTests : IClassFixture<MlcWebApplicationFactory>
{
    private readonly MlcWebApplicationFactory _factory;

    public LoginRateLimitFloodTests(MlcWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Тело запроса — намеренно невалидное (нет БД-пользователя), но для rate-limit теста
    // важен только HTTP-статус ответа. Rate-limiter срабатывает ДО тела хендлера.
    private static readonly LoginPayload TestPayload = new("test-user", "Test-Password-123!");

    [Fact]
    public async Task Login_flood_from_single_ip_triggers_429()
    {
        // Каждый WebApplicationFactory.CreateClient создаёт клиента с одним внутренним IP
        // (127.0.0.1 в TestServer). Делаем 12 запросов — больше PermitLimit=10.
        // Хотя бы один должен вернуть 429.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        const int requestCount = 12;
        var statuses = new List<HttpStatusCode>(requestCount);

        for (var i = 0; i < requestCount; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", TestPayload);
            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests,
            $"при {requestCount} запросах (PermitLimit=10) хотя бы один должен получить 429");
    }

    // Вспомогательная запись для сериализации тела запроса
    private sealed record LoginPayload(string UserName, string Password);
}

// Отдельный класс — отдельная MlcWebApplicationFactory → свежий rate-limiter state.
[Collection("WebApp")]
public sealed class LoginRateLimitSingleTests : IClassFixture<MlcWebApplicationFactory>
{
    private readonly MlcWebApplicationFactory _factory;

    public LoginRateLimitSingleTests(MlcWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly LoginPayload TestPayload = new("test-user", "Test-Password-123!");

    [Fact]
    public async Task First_login_request_is_not_rate_limited()
    {
        // Первый запрос в пределах лимита должен пройти к хендлеру (400/401 от бизнес-логики,
        // не 429). Это подтверждает, что rate-limiter не блокирует штатный путь.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Делаем один запрос — ожидаем НЕ 429 (400 — невалидные данные, или 401 — неверный пароль)
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", TestPayload);

        // Любой не-429 ответ подтверждает, что rate-limiter пропустил запрос к хендлеру.
        // В тест-среде без реальной БД: LoginAsync может вернуть 400 (невалидные данные),
        // 401 (неверные данные), 500 (InMemory-провайдер без миграций). Нас интересует
        // только то, что запрос ПРОШЁЛ к хендлеру, а не был отклонён rate-limiter'ом (429).
        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            "первый запрос в пределах PermitLimit не должен получать 429");
    }

    private sealed record LoginPayload(string UserName, string Password);
}
