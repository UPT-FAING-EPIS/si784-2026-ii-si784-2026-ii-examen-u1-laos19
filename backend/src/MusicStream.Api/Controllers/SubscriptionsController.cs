using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicStream.Api.Dtos;
using MusicStream.Api.Infrastructure;
using MusicStream.Api.Services;

namespace MusicStream.Api.Controllers;

[ApiController]
[Route("subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptions;

    public SubscriptionsController(ISubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    /// <summary>Crea una suscripción o renueva la vigente si es del mismo plan.</summary>
    [HttpPost]
    public async Task<ActionResult<SubscriptionDto>> CreateOrRenew([FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var userId = request.UserId ?? User.GetUserId();
        User.EnsureSelfOrAdmin(userId);
        var subscription = await _subscriptions.CreateOrRenewAsync(userId, request.PlanId!.Value, request.AutoRenew, ct);
        return StatusCode(StatusCodes.Status201Created, subscription);
    }

    /// <summary>Suscripción activa de un usuario.</summary>
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<SubscriptionDto>> GetActive(Guid userId, CancellationToken ct)
    {
        User.EnsureSelfOrAdmin(userId);
        var subscription = await _subscriptions.GetActiveAsync(userId, ct);
        return subscription is null ? NotFound(new ProblemDetails { Status = 404, Title = "El usuario no tiene una suscripción activa." }) : Ok(subscription);
    }

    /// <summary>Cancela una suscripción.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<SubscriptionDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _subscriptions.CancelAsync(id, User.GetUserId(), User.IsAdmin(), ct));

    /// <summary>Lista todas las suscripciones (administración).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<SubscriptionDto>>> List([FromQuery] string? status, CancellationToken ct)
        => Ok(await _subscriptions.ListAsync(status, ct));
}
