using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

public interface IPlanService
{
    Task<IReadOnlyList<PlanDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<PlanDto> CreateAsync(UpsertPlanRequest request, CancellationToken ct = default);
    Task<PlanDto> UpdateAsync(Guid id, UpsertPlanRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}

public class PlanService : IPlanService
{
    private readonly MusicStreamDbContext _db;

    public PlanService(MusicStreamDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlanDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.Plans.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var plans = await query.ToListAsync(ct);
        return plans.OrderBy(p => p.Price).ThenBy(p => p.Name).Select(p => p.ToDto()).ToList();
    }

    public async Task<PlanDto> CreateAsync(UpsertPlanRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (await _db.Plans.AnyAsync(p => p.Name == name, ct))
        {
            throw ServiceException.Conflict("Ya existe un plan con ese nombre.");
        }

        var plan = new Plan();
        Apply(plan, request);
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan.ToDto();
    }

    public async Task<PlanDto> UpdateAsync(Guid id, UpsertPlanRequest request, CancellationToken ct = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ServiceException.NotFound("Plan no encontrado.");
        var name = request.Name.Trim();
        if (await _db.Plans.AnyAsync(p => p.Name == name && p.Id != id, ct))
        {
            throw ServiceException.Conflict("Ya existe un plan con ese nombre.");
        }

        Apply(plan, request);
        await _db.SaveChangesAsync(ct);
        return plan.ToDto();
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        if (id == DbSeeder.FreePlanId)
        {
            throw ServiceException.BadRequest("El plan gratuito no puede desactivarse.");
        }

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ServiceException.NotFound("Plan no encontrado.");
        plan.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(Plan plan, UpsertPlanRequest request)
    {
        plan.Name = request.Name.Trim();
        plan.Description = request.Description.Trim();
        plan.Price = decimal.Round(request.Price, 2);
        plan.DurationDays = request.DurationDays;
        plan.MaxPlaylists = request.MaxPlaylists;
        plan.AllowsPremiumContent = request.AllowsPremiumContent;
        plan.IsActive = request.IsActive;
    }
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> ListAsync(string? search, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, Guid requesterId, CancellationToken ct = default);
}

public class UserAdminService : IUserAdminService
{
    private readonly MusicStreamDbContext _db;

    public UserAdminService(MusicStreamDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.ToLower().Contains(term));
        }

        var users = await query.OrderBy(u => u.Email).Take(500).ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, Guid requesterId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");

        if (id == requesterId && (request.IsActive == false || request.Role == nameof(UserRole.User)))
        {
            throw ServiceException.BadRequest("No puede deshabilitarse ni quitarse el rol de administrador a sí mismo.");
        }

        if (request.IsActive is not null)
        {
            user.IsActive = request.IsActive.Value;
        }

        if (request.Role is not null)
        {
            user.Role = Enum.Parse<UserRole>(request.Role);
        }

        await _db.SaveChangesAsync(ct);
        return user.ToDto();
    }
}
