using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicStream.Api.Dtos;
using MusicStream.Api.Infrastructure;
using MusicStream.Api.Services;

namespace MusicStream.Api.Controllers;

[ApiController]
[Authorize]
public class ListenerController : ControllerBase
{
    private readonly IRecommendationService _recommendations;

    public ListenerController(IRecommendationService recommendations)
    {
        _recommendations = recommendations;
    }

    /// <summary>Historial de reproducción del usuario.</summary>
    [HttpGet("history/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<HistoryItemDto>>> History(Guid userId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        User.EnsureSelfOrAdmin(userId);
        return Ok(await _recommendations.GetHistoryAsync(userId, limit, ct));
    }

    /// <summary>Recomendaciones personalizadas para el usuario.</summary>
    [HttpGet("recommendations/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> Recommendations(Guid userId, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        User.EnsureSelfOrAdmin(userId);
        return Ok(await _recommendations.GetRecommendationsAsync(userId, limit, ct));
    }
}
