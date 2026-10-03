using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MusicStream.Api.Dtos;

namespace MusicStream.Tests.Integration;

/// <summary>Levanta la API en memoria con una base SQLite propia.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@musicstream.test";

    private readonly string _dbPath = Path.Combine(AppContext.BaseDirectory, $"it-{Guid.NewGuid():N}.db");

    public string AdminSecret { get; } = $"Adm{Guid.NewGuid():N}9";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminSecret);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
    }

    public async Task<(HttpClient Client, AuthResponse Auth)> CreateUserClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"user{Guid.NewGuid():N}@test.pe",
            fullName = "Usuario Prueba",
            password = $"Clave{Guid.NewGuid():N}1"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    public async Task<(HttpClient Client, AuthResponse Auth)> CreateAdminClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email = AdminEmail, password = AdminSecret });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
