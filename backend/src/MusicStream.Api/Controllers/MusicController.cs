using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicStream.Api.Dtos;
using MusicStream.Api.Infrastructure;
using MusicStream.Api.Services;

namespace MusicStream.Api.Controllers;

[ApiController]
[Route("music")]
public class MusicController : ControllerBase
{
    private readonly ICatalogService _catalog;

    public MusicController(ICatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lista el catálogo de música (artistas, álbumes y canciones) con búsqueda y filtros.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<CatalogItemDto>>> List(
        [FromQuery] string? search, [FromQuery] string? type, [FromQuery] string? genre,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _catalog.SearchAsync(search, type, genre, page, pageSize, ct));

    /// <summary>Géneros disponibles en el catálogo.</summary>
    [HttpGet("genres")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<string>>> Genres(CancellationToken ct)
        => Ok(await _catalog.GetGenresAsync(ct));

    /// <summary>Detalle de una canción, álbum o artista.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<MusicDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _catalog.GetDetailAsync(id, ct));

    /// <summary>Solicita la reproducción en streaming de una canción y la registra en el historial.</summary>
    [HttpPost("{id:guid}/play")]
    [Authorize]
    public async Task<ActionResult<PlayResponse>> Play(Guid id, CancellationToken ct)
        => Ok(await _catalog.PlayAsync(id, User.GetUserId(), ct));

    [HttpPost("artists")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ArtistDto>> CreateArtist([FromBody] CreateArtistRequest request, CancellationToken ct)
    {
        var artist = await _catalog.CreateArtistAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = artist.Id }, artist);
    }

    [HttpPost("albums")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AlbumDto>> CreateAlbum([FromBody] CreateAlbumRequest request, CancellationToken ct)
    {
        var album = await _catalog.CreateAlbumAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = album.Id }, album);
    }

    [HttpPost("songs")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SongDto>> CreateSong([FromBody] CreateSongRequest request, CancellationToken ct)
    {
        var song = await _catalog.CreateSongAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = song.Id }, song);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _catalog.DeleteAsync(id, ct);
        return NoContent();
    }
}
