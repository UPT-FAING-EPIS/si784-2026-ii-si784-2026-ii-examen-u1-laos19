namespace MusicStream.Api.Domain;

/// <summary>Rol de un usuario dentro de la plataforma.</summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}

/// <summary>Estado del ciclo de vida de una suscripción.</summary>
public enum SubscriptionStatus
{
    Active = 0,
    Cancelled = 1,
    Expired = 2
}

/// <summary>Tipos de reporte que puede generar un administrador.</summary>
public enum ReportType
{
    Usage = 0,
    Subscriptions = 1,
    Revenue = 2
}

/// <summary>Usuario registrado en la plataforma.</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
    public ICollection<PlaybackHistory> PlaybackHistory { get; set; } = new List<PlaybackHistory>();
}

/// <summary>Artista musical.</summary>
public class Artist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? ImageUrl { get; set; }

    public ICollection<Album> Albums { get; set; } = new List<Album>();
    public ICollection<Song> Songs { get; set; } = new List<Song>();
}

/// <summary>Álbum publicado por un artista.</summary>
public class Album
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string? CoverUrl { get; set; }
    public Guid ArtistId { get; set; }

    public Artist? Artist { get; set; }
    public ICollection<Song> Songs { get; set; } = new List<Song>();
}

/// <summary>Canción disponible para streaming.</summary>
public class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public Guid ArtistId { get; set; }
    public Guid? AlbumId { get; set; }

    public Artist? Artist { get; set; }
    public Album? Album { get; set; }
}

/// <summary>Plan de suscripción (gratuito o de pago).</summary>
public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int MaxPlaylists { get; set; }
    public bool AllowsPremiumContent { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsFree => Price == 0m;

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

/// <summary>Suscripción de un usuario a un plan.</summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool AutoRenew { get; set; }
    public DateTime? CancelledAt { get; set; }

    public User? User { get; set; }
    public Plan? Plan { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>Pago asociado a la creación o renovación de una suscripción.</summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }

    public Subscription? Subscription { get; set; }
}

/// <summary>Playlist creada por un usuario.</summary>
public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<PlaylistSong> Songs { get; set; } = new List<PlaylistSong>();
}

/// <summary>Relación N:M entre playlists y canciones.</summary>
public class PlaylistSong
{
    public Guid PlaylistId { get; set; }
    public Guid SongId { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; }

    public Playlist? Playlist { get; set; }
    public Song? Song { get; set; }
}

/// <summary>Registro de una reproducción realizada por un usuario.</summary>
public class PlaybackHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SongId { get; set; }
    public DateTime PlayedAt { get; set; }

    public User? User { get; set; }
    public Song? Song { get; set; }
}

/// <summary>Reporte generado por un administrador.</summary>
public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ReportType Type { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public string ResultJson { get; set; } = "{}";

    public User? GeneratedBy { get; set; }
}
