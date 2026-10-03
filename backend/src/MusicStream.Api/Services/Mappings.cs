using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

internal static class Mappings
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.FullName, u.Role.ToString(), u.IsActive, u.CreatedAt);

    public static SongDto ToDto(this Song s) => new(
        s.Id, s.Title, s.ArtistId, s.Artist?.Name ?? string.Empty, s.AlbumId, s.Album?.Title, s.Genre, s.DurationSeconds, s.IsPremium);

    public static PlanDto ToDto(this Plan p) => new(
        p.Id, p.Name, p.Description, p.Price, p.DurationDays, p.MaxPlaylists, p.AllowsPremiumContent, p.IsFree, p.IsActive);

    public static SubscriptionDto ToDto(this Subscription s) => new(
        s.Id, s.UserId, s.User?.Email, s.PlanId, s.Plan?.Name ?? string.Empty, s.Plan?.Price ?? 0m,
        s.Plan?.AllowsPremiumContent ?? false, s.Plan?.MaxPlaylists ?? 0,
        s.StartDate, s.EndDate, s.Status.ToString(), s.AutoRenew, s.CancelledAt);

    public static PlaylistDto ToDto(this Playlist p) => new(
        p.Id, p.UserId, p.Name, p.Description, p.IsPublic, p.CreatedAt,
        p.Songs.OrderBy(ps => ps.Position).Where(ps => ps.Song is not null).Select(ps => ps.Song!.ToDto()).ToList());
}
