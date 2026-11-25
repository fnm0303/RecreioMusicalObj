using System.Net;
using System.Text;

namespace RecreioMusical.Tests.Fakes;

internal sealed record CapturedRequest(
    HttpMethod Method,
    string? Url,
    string? Authorization,
    string? Body);

/// <summary>
/// Handler HTTP que responde conforme a sequência de tentativas, permitindo simular
/// 401, 429 e erros do Spotify sem rede.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CapturedRequest, int, HttpResponseMessage> _responder;
    private int _calls;

    public StubHttpMessageHandler(Func<CapturedRequest, int, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public StubHttpMessageHandler(Func<CapturedRequest, HttpResponseMessage> responder)
        : this((request, _) => responder(request))
    {
    }

    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var captured = new CapturedRequest(
            request.Method,
            request.RequestUri?.AbsoluteUri,
            request.Headers.Authorization?.ToString(),
            body);

        Requests.Add(captured);

        return _responder(captured, _calls++);
    }

    public static HttpResponseMessage Json(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage Status(HttpStatusCode statusCode) =>
        new(statusCode);
}

/// <summary>Fábrica de <see cref="HttpClient"/> reaproveitando o handler simulado.</summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly HttpClient _client;

    public StubHttpClientFactory(HttpMessageHandler handler) => _client = new HttpClient(handler);

    public HttpClient CreateClient(string name) => _client;

    public void Dispose() => _client.Dispose();
}
