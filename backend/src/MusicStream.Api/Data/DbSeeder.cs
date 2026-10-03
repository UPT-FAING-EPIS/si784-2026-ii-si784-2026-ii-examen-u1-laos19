using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Domain;

namespace MusicStream.Api.Data;

/// <summary>Carga datos iniciales (planes, catálogo y administrador).</summary>
public static class DbSeeder
{
    public static readonly Guid FreePlanId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid PremiumMonthlyPlanId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    public static readonly Guid PremiumYearlyPlanId = Guid.Parse("11111111-0000-0000-0000-000000000003");

    private const string AudioBaseUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-";

    public static async Task SeedAsync(MusicStreamDbContext db, IConfiguration configuration, TimeProvider clock, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Plans.AnyAsync(ct))
        {
            db.Plans.AddRange(
                new Plan { Id = FreePlanId, Name = "Free", Description = "Escucha con anuncios, sin contenido premium.", Price = 0m, DurationDays = 30, MaxPlaylists = 3, AllowsPremiumContent = false },
                new Plan { Id = PremiumMonthlyPlanId, Name = "Premium Mensual", Description = "Sin anuncios, contenido premium y playlists ilimitadas.", Price = 19.90m, DurationDays = 30, MaxPlaylists = 100, AllowsPremiumContent = true },
                new Plan { Id = PremiumYearlyPlanId, Name = "Premium Anual", Description = "Todo Premium con 2 meses de regalo.", Price = 199.00m, DurationDays = 365, MaxPlaylists = 100, AllowsPremiumContent = true });
        }

        if (!await db.Artists.AnyAsync(ct))
        {
            SeedCatalog(db);
        }

        await db.SaveChangesAsync(ct);
        await SeedAdminAsync(db, configuration, clock, ct);
    }

    private static void SeedCatalog(MusicStreamDbContext db)
    {
        var catalog = new (string Artist, string Genre, string Album, int Year, string[] Songs)[]
        {
            ("Andes Sound Collective", "Andina", "Cumbres", 2021, new[] { "Viento del Misti", "Lago Sagrado", "Camino Inca", "Valle Escondido" }),
            ("Neon Pacific", "Electrónica", "Costa Digital", 2023, new[] { "Pulso Nocturno", "Marea Sintética", "Horizonte 404", "Arena Binaria" }),
            ("Los Riffs del Sur", "Rock", "Tacna Eléctrica", 2019, new[] { "Frontera", "Desierto en Llamas", "Ruta 1", "Último Tren" }),
            ("Luna Jazz Trío", "Jazz", "Medianoche en Lima", 2022, new[] { "Barranco Blues", "Garúa", "Malecón", "Café de las Tres" }),
        };

        var trackNumber = 1;
        foreach (var (artistName, genre, albumTitle, year, songs) in catalog)
        {
            var artist = new Artist { Name = artistName, Genre = genre, Bio = $"{artistName} es un proyecto de {genre.ToLowerInvariant()} independiente." };
            var album = new Album { Title = albumTitle, ReleaseYear = year, Artist = artist };
            db.Artists.Add(artist);
            db.Albums.Add(album);
            for (var i = 0; i < songs.Length; i++)
            {
                db.Songs.Add(new Song
                {
                    Title = songs[i],
                    Genre = genre,
                    DurationSeconds = 180 + (trackNumber * 7 % 120),
                    AudioUrl = $"{AudioBaseUrl}{trackNumber}.mp3",
                    IsPremium = i == songs.Length - 1,
                    Artist = artist,
                    Album = album
                });
                trackNumber++;
            }
        }
    }

    private static async Task SeedAdminAsync(MusicStreamDbContext db, IConfiguration configuration, TimeProvider clock, CancellationToken ct)
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        email = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return;
        }

        var admin = new User
        {
            Email = email,
            FullName = "Administrador",
            Role = UserRole.Admin,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
    }
}
