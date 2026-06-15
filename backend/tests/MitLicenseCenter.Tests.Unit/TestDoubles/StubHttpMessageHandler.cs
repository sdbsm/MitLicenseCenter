using System.Net;

namespace MitLicenseCenter.Tests.Unit.TestDoubles;

// MLC-176 — первый тест-дубль HttpMessageHandler в проекте. Позволяет прогнать
// GitHubReleaseClient против синтетических ответов без сети: либо фиксированный
// статус+тело, либо произвольная функция (для эмуляции брошенного исключения /
// таймаута). Захватывает последний запрошенный URI для проверки маршрута.
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static StubHttpMessageHandler Returning(HttpStatusCode status, string? body = null) =>
        new((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body ?? string.Empty),
        });

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new((_, _) => throw exception);

    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(_responder(request, cancellationToken));
    }
}
