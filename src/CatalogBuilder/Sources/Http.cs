using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatalogBuilder.Sources;

/// <summary>Polite HTTP access: identifying User-Agent, retries with backoff on transient errors.</summary>
public sealed class Http : IDisposable
{
    private readonly HttpClient _client;

    public Http(HttpMessageHandler? handler = null)
    {
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _client.Timeout = TimeSpan.FromSeconds(120);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("PokemonLibrary-CatalogBuilder/0.1 (+https://github.com/MPelzer)");
    }

    public Task<JsonNode> GetJsonAsync(string url, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);

    public Task<JsonNode> PostGraphQlAsync(string url, string query, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { query }) }, ct);

    private async Task<JsonNode> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = createRequest();
                using var response = await _client.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new HttpRequestException($"404 for {request.RequestUri}", null, HttpStatusCode.NotFound);
                if (IsTransient(response.StatusCode) && attempt < 4)
                {
                    await Task.Delay(Backoff(attempt), ct);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonNode.ParseAsync(stream, cancellationToken: ct)
                       ?? throw new JsonException($"Empty JSON from {request.RequestUri}");
            }
            catch (Exception ex) when (attempt < 4 && ex is TaskCanceledException or HttpRequestException { StatusCode: null } && !ct.IsCancellationRequested)
            {
                await Task.Delay(Backoff(attempt), ct);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode code) => code is HttpStatusCode.TooManyRequests || (int)code >= 500;

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    public void Dispose() => _client.Dispose();
}
