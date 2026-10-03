using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicStream.Api.Dtos;
using MusicStream.Api.Infrastructure;
using MusicStream.Api.Services;

namespace MusicStream.Api.Controllers;

[ApiController]
[Route("playlists")]
[Authorize]
public class PlaylistsController : ControllerBase
{
    private readonly IPlaylistService _playlists;

    public PlaylistsController(IPlaylistService playlists)
    {
        _playlists = playlists;
    }

    /// <summary>Crea una playlist para el usuario autenticado.</summary>
    [HttpPost]
    public async Task<ActionResult<PlaylistDto>> Create([FromBody] CreatePlaylistRequest request, CancellationToken ct)
    {
        var playlist = await _playlists.CreateAsync(User.GetUserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, playlist);
    }

    /// <summary>Lista las playlists de un usuario.</summary>
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<PlaylistDto>>> GetByUser(Guid userId, CancellationToken ct)
    {
        User.EnsureSelfOrAdmin(userId);
        return Ok(await _playlists.GetByUserAsync(userId, ct));
    }

    [HttpPost("{id:guid}/songs")]
    public async Task<ActionResult<PlaylistDto>> AddSong(Guid id, [FromBody] AddSongRequest request, CancellationToken ct)
        => Ok(await _playlists.AddSongAsync(id, request.SongId!.Value, User.GetUserId(), ct));

    [HttpDelete("{id:guid}/songs/{songId:guid}")]
    public async Task<ActionResult<PlaylistDto>> RemoveSong(Guid id, Guid songId, CancellationToken ct)
        => Ok(await _playlists.RemoveSongAsync(id, songId, User.GetUserId(), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _playlists.DeleteAsync(id, User.GetUserId(), User.IsAdmin(), ct);
        return NoContent();
    }
}
