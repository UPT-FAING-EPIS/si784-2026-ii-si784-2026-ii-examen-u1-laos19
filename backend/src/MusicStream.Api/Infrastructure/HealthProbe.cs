namespace MusicStream.Api.Infrastructure;

/// <summary>Sonda HTTP local usada por el HEALTHCHECK del contenedor.</summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync(HttpMessageHandler? handler = null)
    {
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(3);
        try
        {
            using var response = await client.GetAsync(new Uri($"http://localhost:{port}/health"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }
}
