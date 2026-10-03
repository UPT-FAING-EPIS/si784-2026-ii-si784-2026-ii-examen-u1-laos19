using System.Security.Claims;
using MusicStream.Api.Domain;
using MusicStream.Api.Services;

namespace MusicStream.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw ServiceException.Unauthorized("Token inválido.");
    }

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(nameof(UserRole.Admin));

    /// <summary>Verifica que el usuario autenticado sea el propietario del recurso o un administrador.</summary>
    public static void EnsureSelfOrAdmin(this ClaimsPrincipal principal, Guid userId)
    {
        if (principal.GetUserId() != userId && !principal.IsAdmin())
        {
            throw ServiceException.Forbidden("No tiene permisos sobre este usuario.");
        }
    }
}
