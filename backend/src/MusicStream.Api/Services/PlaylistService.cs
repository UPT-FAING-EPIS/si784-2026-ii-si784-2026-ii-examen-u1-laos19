using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

public interface IPlaylistService
{
    Task<PlaylistDto> CreateAsync(Guid userId, CreatePlaylistRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PlaylistDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<PlaylistDto> AddSongAsync(Guid playlistId, Guid songId, Guid requesterId, CancellationToken ct = default);
    Task<PlaylistDto> RemoveSongAsync(Guid playlistId, Guid songId, Guid requesterId, CancellationToken ct = default);
    Task DeleteAsync(Guid playlistId, Guid requesterId, bool requesterIsAdmin, CancellationToken ct = default);
}

public class PlaylistService : IPlaylistService
{
    private readonly MusicStreamDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly TimeProvider _clock;

    public PlaylistService(MusicStreamDbContext db, ISubscriptionService subscriptions, TimeProvider clock)
    {
        _db = db;
        _subscriptions = subscriptions;
        _clock = clock;
    }

    public async Task<PlaylistDto> CreateAsync(Guid userId, CreatePlaylistRequest request, CancellationToken ct = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            throw ServiceException.NotFound("Usuario no encontrado.");
        }

        var plan = await _subscriptions.GetEffectivePlanAsync(userId, ct);
        var current = await _db.Playlists.CountAsync(p => p.UserId == userId, ct);
        if (current >= plan.MaxPlaylists)
        {
            throw ServiceException.Conflict($"Su plan {plan.Name} permite un máximo de {plan.MaxPlaylists} playlists.");
        }

        var name = request.Name.Trim();
        if (await _db.Playlists.AnyAsync(p => p.UserId == userId && p.Name == name, ct))
        {
            throw ServiceException.Conflict("Ya existe una playlist con ese nombre.");
        }

        var songIds = request.SongIds.Distinct().ToList();
        var songs = songIds.Count == 0
            ? new List<Song>()
            : await _db.Songs.Include(s => s.Artist).Include(s => s.Album).Where(s => songIds.Contains(s.Id)).ToListAsync(ct);
        if (songs.Count != songIds.Count)
        {
            throw ServiceException.BadRequest("Una o más canciones no existen.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var playlist = new Playlist
        {
            UserId = userId,
            Name = name,
            Description = request.Description?.Trim(),
            IsPublic = request.IsPublic,
            CreatedAt = now
        };
        var position = 1;
        foreach (var id in songIds)
        {
            playlist.Songs.Add(new PlaylistSong { SongId = id, Position = position++, AddedAt = now, Song = songs.First(s => s.Id == id) });
        }

        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(ct);
        return playlist.ToDto();
    }

    public async Task<IReadOnlyList<PlaylistDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var playlists = await _db.Playlists.AsNoTracking()
            .Include(p => p.Songs).ThenInclude(ps => ps.Song!).ThenInclude(s => s.Artist)
            .Include(p => p.Songs).ThenInclude(ps => ps.Song!).ThenInclude(s => s.Album)
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
        return playlists.Select(p => p.ToDto()).ToList();
    }

    public async Task<PlaylistDto> AddSongAsync(Guid playlistId, Guid songId, Guid requesterId, CancellationToken ct = default)
    {
        var playlist = await LoadOwnedAsync(playlistId, requesterId, ct);
        if (!await _db.Songs.AnyAsync(s => s.Id == songId, ct))
        {
            throw ServiceException.NotFound("Canción no encontrada.");
        }

        if (playlist.Songs.Any(ps => ps.SongId == songId))
        {
            throw ServiceException.Conflict("La canción ya está en la playlist.");
        }

        var position = playlist.Songs.Count == 0 ? 1 : playlist.Songs.Max(ps => ps.Position) + 1;
        _db.PlaylistSongs.Add(new PlaylistSong { PlaylistId = playlist.Id, SongId = songId, Position = position, AddedAt = _clock.GetUtcNow().UtcDateTime });
        await _db.SaveChangesAsync(ct);
        return (await LoadOwnedAsync(playlistId, requesterId, ct)).ToDto();
    }

    public async Task<PlaylistDto> RemoveSongAsync(Guid playlistId, Guid songId, Guid requesterId, CancellationToken ct = default)
    {
        var playlist = await LoadOwnedAsync(playlistId, requesterId, ct);
        var entry = playlist.Songs.FirstOrDefault(ps => ps.SongId == songId)
            ?? throw ServiceException.NotFound("La canción no está en la playlist.");
        _db.PlaylistSongs.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return playlist.ToDto();
    }

    public async Task DeleteAsync(Guid playlistId, Guid requesterId, bool requesterIsAdmin, CancellationToken ct = default)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, ct)
            ?? throw ServiceException.NotFound("Playlist no encontrada.");
        if (!requesterIsAdmin && playlist.UserId != requesterId)
        {
            throw ServiceException.Forbidden("No puede eliminar la playlist de otro usuario.");
        }

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Playlist> LoadOwnedAsync(Guid playlistId, Guid requesterId, CancellationToken ct)
    {
        var playlist = await _db.Playlists
            .Include(p => p.Songs).ThenInclude(ps => ps.Song!).ThenInclude(s => s.Artist)
            .Include(p => p.Songs).ThenInclude(ps => ps.Song!).ThenInclude(s => s.Album)
            .FirstOrDefaultAsync(p => p.Id == playlistId, ct)
            ?? throw ServiceException.NotFound("Playlist no encontrada.");
        if (playlist.UserId != requesterId)
        {
            throw ServiceException.Forbidden("Solo el propietario puede modificar la playlist.");
        }

        return playlist;
    }
}
