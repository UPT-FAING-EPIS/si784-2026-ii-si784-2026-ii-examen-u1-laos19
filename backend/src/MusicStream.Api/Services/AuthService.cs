using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;
using MusicStream.Api.Infrastructure;

namespace MusicStream.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly MusicStreamDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly TimeProvider _clock;
    private readonly JwtOptions _jwt;
    private readonly ISubscriptionService _subscriptions;

    public AuthService(MusicStreamDbContext db, IPasswordHasher<User> hasher, TimeProvider clock, IOptions<JwtOptions> jwt, ISubscriptionService subscriptions)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _jwt = jwt.Value;
        _subscriptions = subscriptions;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw ServiceException.Conflict("El correo ya está registrado.");
        }

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Role = UserRole.User,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Todo usuario nuevo inicia con el plan gratuito.
        await _subscriptions.CreateOrRenewAsync(user.Id, DbSeeder.FreePlanId, autoRenew: true, ct);

        return CreateToken(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw ServiceException.Unauthorized("Credenciales inválidas.");
        }

        if (!user.IsActive)
        {
            throw ServiceException.Forbidden("La cuenta está deshabilitada.");
        }

        return CreateToken(user);
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");
        return user.ToDto();
    }

    internal AuthResponse CreateToken(User user)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwt.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, now, expires, credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user.ToDto());
    }
}
