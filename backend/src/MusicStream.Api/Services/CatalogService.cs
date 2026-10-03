using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

public interface ICatalogService
{
    Task<PagedResult<CatalogItemDto>> SearchAsync(string? search, string? type, string? genre, int page, int pageSize, CancellationToken ct = default);
    Task<MusicDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<PlayResponse> PlayAsync(Guid songId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetGenresAsync(CancellationToken ct = default);
    Task<ArtistDto> CreateArtistAsync(CreateArtistRequest request, CancellationToken ct = default);
    Task<AlbumDto> CreateAlbumAsync(CreateAlbumRequest request, CancellationToken ct = default);
    Task<SongDto> CreateSongAsync(CreateSongRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class CatalogService : ICatalogService
{
    public const string TypeSong = "song";
    public const string TypeAlbum = "album";
    public const string TypeArtist = "artist";

    private readonly MusicStreamDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly TimeProvider _clock;

    public CatalogService(MusicStreamDbContext db, ISubscriptionService subscriptions, TimeProvider clock)
    {
        _db = db;
        _subscriptions = subscriptions;
        _clock = clock;
    }

    public async Task<PagedResult<CatalogItemDto>> SearchAsync(string? search, string? type, string? genre, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedType = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToLowerInvariant();
        if (normalizedType is not null and not (TypeSong or TypeAlbum or TypeArtist))
        {
            throw ServiceException.BadRequest("Tipo inválido. Use song, album o artist.");
        }

        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
        var genreTerm = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim().ToLowerInvariant();
        var items = new List<CatalogItemDto>();

        if (normalizedType is null or TypeArtist)
        {
            var artists = _db.Artists.AsNoTracking();
            if (term is not null) artists = artists.Where(a => a.Name.ToLower().Contains(term));
            if (genreTerm is not null) artists = artists.Where(a => a.Genre.ToLower() == genreTerm);
            items.AddRange(await artists
                .Select(a => new CatalogItemDto(a.Id, TypeArtist, a.Name, "Artista", a.Genre, false, null))
                .ToListAsync(ct));
        }

        if (normalizedType is null or TypeAlbum)
        {
            var albums = _db.Albums.AsNoTracking();
            if (term is not null) albums = albums.Where(a => a.Title.ToLower().Contains(term) || a.Artist!.Name.ToLower().Contains(term));
            if (genreTerm is not null) albums = albums.Where(a => a.Artist!.Genre.ToLower() == genreTerm);
            items.AddRange(await albums
                .Select(a => new CatalogItemDto(a.Id, TypeAlbum, a.Title, a.Artist!.Name + " · " + a.ReleaseYear, a.Artist.Genre, false, null))
                .ToListAsync(ct));
        }

        if (normalizedType is null or TypeSong)
        {
            var songs = _db.Songs.AsNoTracking();
            if (term is not null) songs = songs.Where(s => s.Title.ToLower().Contains(term) || s.Artist!.Name.ToLower().Contains(term));
            if (genreTerm is not null) songs = songs.Where(s => s.Genre.ToLower() == genreTerm);
            items.AddRange(await songs
                .Select(s => new CatalogItemDto(s.Id, TypeSong, s.Title, s.Artist!.Name, s.Genre, s.IsPremium, s.DurationSeconds))
                .ToListAsync(ct));
        }

        var ordered = items.OrderBy(i => i.Type).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase).ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<CatalogItemDto>(pageItems, page, pageSize, ordered.Count);
    }

    public async Task<MusicDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var song = await _db.Songs.AsNoTracking().Include(s => s.Artist).Include(s => s.Album)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (song is not null)
        {
            return new MusicDetailDto(TypeSong, song.ToDto(), null, null);
        }

        var album = await _db.Albums.AsNoTracking().Include(a => a.Artist)
            .Include(a => a.Songs).ThenInclude(s => s.Artist)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (album is not null)
        {
            var songs = album.Songs.OrderBy(s => s.Title).Select(s => { s.Album = album; return s.ToDto(); }).ToList();
            return new MusicDetailDto(TypeAlbum, null,
                new AlbumDto(album.Id, album.Title, album.ReleaseYear, album.ArtistId, album.Artist?.Name ?? string.Empty, album.CoverUrl, songs), null);
        }

        var artist = await _db.Artists.AsNoTracking().Include(a => a.Albums)
            .Include(a => a.Songs).ThenInclude(s => s.Album)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (artist is not null)
        {
            var albums = artist.Albums.OrderByDescending(a => a.ReleaseYear).Select(a => new AlbumSummaryDto(a.Id, a.Title, a.ReleaseYear)).ToList();
            var songs = artist.Songs.OrderBy(s => s.Title).Select(s => { s.Artist = artist; return s.ToDto(); }).ToList();
            return new MusicDetailDto(TypeArtist, null, null,
                new ArtistDto(artist.Id, artist.Name, artist.Genre, artist.Bio, artist.ImageUrl, albums, songs));
        }

        throw ServiceException.NotFound("Elemento del catálogo no encontrado.");
    }

    public async Task<PlayResponse> PlayAsync(Guid songId, Guid userId, CancellationToken ct = default)
    {
        var song = await _db.Songs.AsNoTracking().Include(s => s.Artist).FirstOrDefaultAsync(s => s.Id == songId, ct)
            ?? throw ServiceException.NotFound("Canción no encontrada.");

        if (song.IsPremium)
        {
            var plan = await _subscriptions.GetEffectivePlanAsync(userId, ct);
            if (!plan.AllowsPremiumContent)
            {
                throw ServiceException.Forbidden("Esta canción requiere un plan Premium.");
            }
        }

        _db.PlaybackHistory.Add(new PlaybackHistory { UserId = userId, SongId = songId, PlayedAt = _clock.GetUtcNow().UtcDateTime });
        await _db.SaveChangesAsync(ct);
        return new PlayResponse(song.Id, song.Title, song.Artist?.Name ?? string.Empty, song.AudioUrl);
    }

    public async Task<IReadOnlyList<string>> GetGenresAsync(CancellationToken ct = default)
    {
        return await _db.Artists.AsNoTracking().Select(a => a.Genre).Distinct().OrderBy(g => g).ToListAsync(ct);
    }

    public async Task<ArtistDto> CreateArtistAsync(CreateArtistRequest request, CancellationToken ct = default)
    {
        var artist = new Artist
        {
            Name = request.Name.Trim(),
            Genre = request.Genre.Trim(),
            Bio = request.Bio?.Trim(),
            ImageUrl = request.ImageUrl?.Trim()
        };
        _db.Artists.Add(artist);
        await _db.SaveChangesAsync(ct);
        return new ArtistDto(artist.Id, artist.Name, artist.Genre, artist.Bio, artist.ImageUrl, Array.Empty<AlbumSummaryDto>(), Array.Empty<SongDto>());
    }

    public async Task<AlbumDto> CreateAlbumAsync(CreateAlbumRequest request, CancellationToken ct = default)
    {
        var artist = await _db.Artists.FirstOrDefaultAsync(a => a.Id == request.ArtistId, ct)
            ?? throw ServiceException.NotFound("Artista no encontrado.");
        var album = new Album
        {
            Title = request.Title.Trim(),
            ReleaseYear = request.ReleaseYear,
            ArtistId = artist.Id,
            CoverUrl = request.CoverUrl?.Trim()
        };
        _db.Albums.Add(album);
        await _db.SaveChangesAsync(ct);
        return new AlbumDto(album.Id, album.Title, album.ReleaseYear, artist.Id, artist.Name, album.CoverUrl, Array.Empty<SongDto>());
    }

    public async Task<SongDto> CreateSongAsync(CreateSongRequest request, CancellationToken ct = default)
    {
        var artist = await _db.Artists.FirstOrDefaultAsync(a => a.Id == request.ArtistId, ct)
            ?? throw ServiceException.NotFound("Artista no encontrado.");
        Album? album = null;
        if (request.AlbumId is not null)
        {
            album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == request.AlbumId, ct)
                ?? throw ServiceException.NotFound("Álbum no encontrado.");
            if (album.ArtistId != artist.Id)
            {
                throw ServiceException.BadRequest("El álbum no pertenece al artista indicado.");
            }
        }

        if (!Uri.TryCreate(request.AudioUrl, UriKind.Absolute, out var audioUri) || audioUri.Scheme != Uri.UriSchemeHttps)
        {
            throw ServiceException.BadRequest("La URL de audio debe ser HTTPS.");
        }

        var song = new Song
        {
            Title = request.Title.Trim(),
            ArtistId = artist.Id,
            AlbumId = album?.Id,
            Genre = request.Genre.Trim(),
            DurationSeconds = request.DurationSeconds,
            AudioUrl = audioUri.ToString(),
            IsPremium = request.IsPremium,
            Artist = artist,
            Album = album
        };
        _db.Songs.Add(song);
        await _db.SaveChangesAsync(ct);
        return song.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var song = await _db.Songs.FindAsync(new object[] { id }, ct);
        if (song is not null)
        {
            _db.Songs.Remove(song);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var album = await _db.Albums.FindAsync(new object[] { id }, ct);
        if (album is not null)
        {
            _db.Albums.Remove(album);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var artist = await _db.Artists.FindAsync(new object[] { id }, ct);
        if (artist is not null)
        {
            _db.Artists.Remove(artist);
            await _db.SaveChangesAsync(ct);
            return;
        }

        throw ServiceException.NotFound("Elemento del catálogo no encontrado.");
    }
}
