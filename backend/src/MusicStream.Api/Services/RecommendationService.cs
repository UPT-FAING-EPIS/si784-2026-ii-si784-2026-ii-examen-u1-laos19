using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

public interface IRecommendationService
{
    Task<IReadOnlyList<HistoryItemDto>> GetHistoryAsync(Guid userId, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<RecommendationDto>> GetRecommendationsAsync(Guid userId, int limit, CancellationToken ct = default);
}

/// <summary>
/// Recomendaciones basadas en contenido: prioriza los géneros y artistas más escuchados por el usuario
/// y completa con las canciones más populares de la plataforma.
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly MusicStreamDbContext _db;

    public RecommendationService(MusicStreamDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<HistoryItemDto>> GetHistoryAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var history = await _db.PlaybackHistory.AsNoTracking()
            .Include(h => h.Song!).ThenInclude(s => s.Artist)
            .Include(h => h.Song!).ThenInclude(s => s.Album)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.PlayedAt)
            .Take(limit)
            .ToListAsync(ct);
        return history.Where(h => h.Song is not null).Select(h => new HistoryItemDto(h.Id, h.Song!.ToDto(), h.PlayedAt)).ToList();
    }

    public async Task<IReadOnlyList<RecommendationDto>> GetRecommendationsAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var userPlays = await _db.PlaybackHistory.AsNoTracking()
            .Where(h => h.UserId == userId)
            .Select(h => new { h.SongId, h.Song!.Genre, h.Song.ArtistId })
            .ToListAsync(ct);

        var playedIds = userPlays.Select(p => p.SongId).ToHashSet();
        var genreScore = userPlays.GroupBy(p => p.Genre).ToDictionary(g => g.Key, g => g.Count());
        var artistScore = userPlays.GroupBy(p => p.ArtistId).ToDictionary(g => g.Key, g => g.Count());

        var popularity = await _db.PlaybackHistory.AsNoTracking()
            .GroupBy(h => h.SongId)
            .Select(g => new { SongId = g.Key, Plays = g.Count() })
            .ToDictionaryAsync(x => x.SongId, x => x.Plays, ct);

        var candidates = await _db.Songs.AsNoTracking().Include(s => s.Artist).Include(s => s.Album).ToListAsync(ct);

        return candidates
            .Where(s => !playedIds.Contains(s.Id))
            .Select(s =>
            {
                var g = genreScore.GetValueOrDefault(s.Genre);
                var a = artistScore.GetValueOrDefault(s.ArtistId);
                var p = popularity.GetValueOrDefault(s.Id);
                var score = (a * 3) + (g * 2) + p;
                string reason;
                if (a > 0)
                {
                    reason = $"Porque escuchas a {s.Artist?.Name}";
                }
                else if (g > 0)
                {
                    reason = $"Porque te gusta el género {s.Genre}";
                }
                else
                {
                    reason = "Popular en MusicStream";
                }

                return new { Song = s, Score = score, Reason = reason };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Song.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new RecommendationDto(x.Song.ToDto(), x.Reason))
            .ToList();
    }
}
