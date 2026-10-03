using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MusicStream.Api.Data;

namespace MusicStream.Tests.Support;

/// <summary>Reloj controlable para pruebas.</summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset? start = null)
    {
        _now = start ?? new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

/// <summary>Base de datos SQLite en memoria con datos semilla, aislada por prueba.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase(FakeClock? clock = null)
    {
        Clock = clock ?? new FakeClock();
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<MusicStreamDbContext>().UseSqlite(_connection).Options;
        Context = new MusicStreamDbContext(options);
        DbSeeder.SeedAsync(Context, new ConfigurationBuilder().Build(), Clock).GetAwaiter().GetResult();
    }

    public FakeClock Clock { get; }

    public MusicStreamDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
