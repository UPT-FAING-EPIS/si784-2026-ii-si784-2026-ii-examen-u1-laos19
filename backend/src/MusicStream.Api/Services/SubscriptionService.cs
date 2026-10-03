using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

public interface ISubscriptionService
{
    Task<SubscriptionDto> CreateOrRenewAsync(Guid userId, Guid planId, bool autoRenew, CancellationToken ct = default);
    Task<SubscriptionDto?> GetActiveAsync(Guid userId, CancellationToken ct = default);
    Task<SubscriptionDto> CancelAsync(Guid subscriptionId, Guid requesterId, bool requesterIsAdmin, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionDto>> ListAsync(string? status, CancellationToken ct = default);
    Task<Plan> GetEffectivePlanAsync(Guid userId, CancellationToken ct = default);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly MusicStreamDbContext _db;
    private readonly TimeProvider _clock;

    public SubscriptionService(MusicStreamDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SubscriptionDto> CreateOrRenewAsync(Guid userId, Guid planId, bool autoRenew, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");
        if (!user.IsActive)
        {
            throw ServiceException.BadRequest("El usuario está deshabilitado.");
        }

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw ServiceException.NotFound("Plan no encontrado.");
        if (!plan.IsActive)
        {
            throw ServiceException.BadRequest("El plan no está disponible.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        await ExpireStaleAsync(userId, now, ct);

        var active = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);

        Subscription subscription;
        if (active is not null && active.PlanId == plan.Id)
        {
            // Renovación: se extiende el periodo vigente.
            active.EndDate = active.EndDate.AddDays(plan.DurationDays);
            active.AutoRenew = autoRenew;
            subscription = active;
        }
        else
        {
            if (active is not null)
            {
                // Cambio de plan: se cierra la suscripción anterior.
                active.Status = SubscriptionStatus.Cancelled;
                active.CancelledAt = now;
                active.AutoRenew = false;
            }

            subscription = new Subscription
            {
                UserId = userId,
                PlanId = plan.Id,
                StartDate = now,
                EndDate = now.AddDays(plan.DurationDays),
                Status = SubscriptionStatus.Active,
                AutoRenew = autoRenew
            };
            _db.Subscriptions.Add(subscription);
        }

        if (plan.Price > 0m)
        {
            _db.Payments.Add(new Payment { SubscriptionId = subscription.Id, Amount = plan.Price, PaidAt = now });
        }

        await _db.SaveChangesAsync(ct);
        subscription.Plan = plan;
        subscription.User = user;
        return subscription.ToDto();
    }

    public async Task<SubscriptionDto?> GetActiveAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            throw ServiceException.NotFound("Usuario no encontrado.");
        }

        await ExpireStaleAsync(userId, _clock.GetUtcNow().UtcDateTime, ct);
        var active = await _db.Subscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);
        return active?.ToDto();
    }

    public async Task<SubscriptionDto> CancelAsync(Guid subscriptionId, Guid requesterId, bool requesterIsAdmin, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw ServiceException.NotFound("Suscripción no encontrada.");

        if (!requesterIsAdmin && subscription.UserId != requesterId)
        {
            throw ServiceException.Forbidden("No puede cancelar la suscripción de otro usuario.");
        }

        if (subscription.Status != SubscriptionStatus.Active)
        {
            throw ServiceException.Conflict("La suscripción no está activa.");
        }

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = _clock.GetUtcNow().UtcDateTime;
        subscription.AutoRenew = false;
        await _db.SaveChangesAsync(ct);
        return subscription.ToDto();
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var query = _db.Subscriptions.AsNoTracking().Include(s => s.Plan).Include(s => s.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SubscriptionStatus>(status, ignoreCase: true, out var parsed))
            {
                throw ServiceException.BadRequest("Estado de suscripción inválido.");
            }

            query = query.Where(s => s.Status == parsed);
        }

        var list = await query.OrderByDescending(s => s.StartDate).Take(500).ToListAsync(ct);
        return list.Select(s => s.ToDto()).ToList();
    }

    public async Task<Plan> GetEffectivePlanAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var active = await _db.Subscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > now, ct);
        if (active?.Plan is not null)
        {
            return active.Plan;
        }

        return await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == DbSeeder.FreePlanId, ct)
            ?? new Plan { Id = DbSeeder.FreePlanId, Name = "Free", MaxPlaylists = 3, DurationDays = 30 };
    }

    /// <summary>Marca como expiradas las suscripciones vencidas; las de renovación automática se renuevan.</summary>
    private async Task ExpireStaleAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var stale = await _db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate <= now)
            .ToListAsync(ct);
        if (stale.Count == 0)
        {
            return;
        }

        foreach (var subscription in stale)
        {
            var plan = subscription.Plan;
            if (subscription.AutoRenew && plan is { IsActive: true })
            {
                subscription.EndDate = now.AddDays(plan.DurationDays);
                if (plan.Price > 0m)
                {
                    _db.Payments.Add(new Payment { SubscriptionId = subscription.Id, Amount = plan.Price, PaidAt = now });
                }
            }
            else
            {
                subscription.Status = SubscriptionStatus.Expired;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
