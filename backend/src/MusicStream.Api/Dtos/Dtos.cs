using System.ComponentModel.DataAnnotations;

namespace MusicStream.Api.Dtos;

// ---------- Autenticación ----------

public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 3)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "La contraseña debe contener letras y números.", MatchTimeoutInMilliseconds = 100)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

public sealed record UserDto(Guid Id, string Email, string FullName, string Role, bool IsActive, DateTime CreatedAt);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------- Catálogo ----------

public sealed record CatalogItemDto(Guid Id, string Type, string Title, string Subtitle, string Genre, bool IsPremium, int? DurationSeconds);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record SongDto(Guid Id, string Title, Guid ArtistId, string ArtistName, Guid? AlbumId, string? AlbumTitle, string Genre, int DurationSeconds, bool IsPremium);

public sealed record AlbumDto(Guid Id, string Title, int ReleaseYear, Guid ArtistId, string ArtistName, string? CoverUrl, IReadOnlyList<SongDto> Songs);

public sealed record ArtistDto(Guid Id, string Name, string Genre, string? Bio, string? ImageUrl, IReadOnlyList<AlbumSummaryDto> Albums, IReadOnlyList<SongDto> TopSongs);

public sealed record AlbumSummaryDto(Guid Id, string Title, int ReleaseYear);

public sealed record MusicDetailDto(string Type, SongDto? Song, AlbumDto? Album, ArtistDto? Artist);

public sealed record PlayResponse(Guid SongId, string Title, string ArtistName, string StreamUrl);

public sealed class CreateArtistRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 2)]
    public string Genre { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Bio { get; set; }

    [Url, StringLength(500)]
    public string? ImageUrl { get; set; }
}

public sealed class CreateAlbumRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Range(1900, 2100)]
    public int ReleaseYear { get; set; }

    [Required]
    public Guid? ArtistId { get; set; }

    [Url, StringLength(500)]
    public string? CoverUrl { get; set; }
}

public sealed class CreateSongRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public Guid? ArtistId { get; set; }

    public Guid? AlbumId { get; set; }

    [Required, StringLength(60, MinimumLength = 2)]
    public string Genre { get; set; } = string.Empty;

    [Range(1, 3600)]
    public int DurationSeconds { get; set; }

    [Required, Url, StringLength(500)]
    public string AudioUrl { get; set; } = string.Empty;

    public bool IsPremium { get; set; }
}

// ---------- Planes y suscripciones ----------

public sealed record PlanDto(Guid Id, string Name, string Description, decimal Price, int DurationDays, int MaxPlaylists, bool AllowsPremiumContent, bool IsFree, bool IsActive);

public sealed class UpsertPlanRequest
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "10000")]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [Range(1, 1000)]
    public int MaxPlaylists { get; set; }

    public bool AllowsPremiumContent { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class CreateSubscriptionRequest
{
    /// <summary>Usuario destino. Solo un administrador puede indicar un usuario distinto al autenticado.</summary>
    public Guid? UserId { get; set; }

    [Required]
    public Guid? PlanId { get; set; }

    public bool AutoRenew { get; set; } = true;
}

public sealed record SubscriptionDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    Guid PlanId,
    string PlanName,
    decimal Price,
    bool AllowsPremiumContent,
    int MaxPlaylists,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    bool AutoRenew,
    DateTime? CancelledAt);

// ---------- Playlists ----------

public sealed class CreatePlaylistRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public List<Guid> SongIds { get; set; } = new();
}

public sealed class AddSongRequest
{
    [Required]
    public Guid? SongId { get; set; }
}

public sealed record PlaylistDto(Guid Id, Guid UserId, string Name, string? Description, bool IsPublic, DateTime CreatedAt, IReadOnlyList<SongDto> Songs);

// ---------- Historial y recomendaciones ----------

public sealed record HistoryItemDto(Guid Id, SongDto Song, DateTime PlayedAt);

public sealed record RecommendationDto(SongDto Song, string Reason);

// ---------- Administración ----------

public sealed class UpdateUserRequest
{
    public bool? IsActive { get; set; }

    [RegularExpression("^(User|Admin)$", ErrorMessage = "Rol inválido.", MatchTimeoutInMilliseconds = 100)]
    public string? Role { get; set; }
}

public sealed class GenerateReportRequest
{
    [Required, RegularExpression("^(Usage|Subscriptions|Revenue)$", ErrorMessage = "Tipo de reporte inválido.", MatchTimeoutInMilliseconds = 100)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public DateTime? From { get; set; }

    [Required]
    public DateTime? To { get; set; }
}

public sealed record ReportDto(Guid Id, string Type, DateTime PeriodStart, DateTime PeriodEnd, DateTime GeneratedAt, Guid GeneratedByUserId, object Result);
