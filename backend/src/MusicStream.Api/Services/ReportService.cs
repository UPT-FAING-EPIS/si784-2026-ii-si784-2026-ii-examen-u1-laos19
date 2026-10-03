using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;

namespace MusicStream.Api.Services;

public interface IReportService
{
    Task<ReportDto> GenerateAsync(GenerateReportRequest request, Guid adminId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportDto>> ListAsync(CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MusicStreamDbContext _db;
    private readonly TimeProvider _clock;

    public ReportService(MusicStreamDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ReportDto> GenerateAsync(GenerateReportRequest request, Guid adminId, CancellationToken ct = default)
    {
        var type = Enum.Parse<ReportType>(request.Type, ignoreCase: true);
        var from = DateTime.SpecifyKind(request.From!.Value.ToUniversalTime(), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.To!.Value.ToUniversalTime(), DateTimeKind.Utc);
        if (from > to)
        {
            throw ServiceException.BadRequest("La fecha inicial debe ser anterior a la fecha final.");
        }

        if ((to - from).TotalDays > 366)
        {
            throw ServiceException.BadRequest("El periodo máximo de un reporte es de un año.");
        }

        object result = type switch
        {
            ReportType.Usage => await BuildUsageAsync(from, to, ct),
            ReportType.Subscriptions => await BuildSubscriptionsAsync(from, to, ct),
            _ => await BuildRevenueAsync(from, to, ct)
        };

        var report = new Report
        {
            Type = type,
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = _clock.GetUtcNow().UtcDateTime,
            GeneratedByUserId = adminId,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions)
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);
        return ToDto(report);
    }

    public async Task<IReadOnlyList<ReportDto>> ListAsync(CancellationToken ct = default)
    {
        var reports = await _db.Reports.AsNoTracking().OrderByDescending(r => r.GeneratedAt).Take(100).ToListAsync(ct);
        return reports.Select(ToDto).ToList();
    }

    internal async Task<object> BuildUsageAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var plays = await _db.PlaybackHistory.AsNoTracking()
            .Where(h => h.PlayedAt >= from && h.PlayedAt <= to)
            .Select(h => new { h.UserId, h.SongId, SongTitle = h.Song!.Title, ArtistName = h.Song.Artist!.Name, h.Song.Genre })
            .ToListAsync(ct);

        return new
        {
            totalPlays = plays.Count,
            activeListeners = plays.Select(p => p.UserId).Distinct().Count(),
            topSongs = plays.GroupBy(p => new { p.SongId, p.SongTitle, p.ArtistName })
                .Select(g => new { songId = g.Key.SongId, title = g.Key.SongTitle, artist = g.Key.ArtistName, plays = g.Count() })
                .OrderByDescending(x => x.plays).Take(10).ToList(),
            playsByGenre = plays.GroupBy(p => p.Genre)
                .Select(g => new { genre = g.Key, plays = g.Count() })
                .OrderByDescending(x => x.plays).ToList()
        };
    }

    internal async Task<object> BuildSubscriptionsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var subs = await _db.Subscriptions.AsNoTracking()
            .Select(s => new { s.Status, s.StartDate, s.CancelledAt, PlanName = s.Plan!.Name })
            .ToListAsync(ct);

        return new
        {
            newSubscriptions = subs.Count(s => s.StartDate >= from && s.StartDate <= to),
            cancellations = subs.Count(s => s.CancelledAt >= from && s.CancelledAt <= to),
            activeNow = subs.Count(s => s.Status == SubscriptionStatus.Active),
            byPlan = subs.Where(s => s.Status == SubscriptionStatus.Active)
                .GroupBy(s => s.PlanName)
                .Select(g => new { plan = g.Key, active = g.Count() })
                .OrderByDescending(x => x.active).ToList(),
            byStatus = subs.GroupBy(s => s.Status.ToString())
                .Select(g => new { status = g.Key, total = g.Count() }).ToList()
        };
    }

    internal async Task<object> BuildRevenueAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        // La suma se realiza en memoria para mantener compatibilidad con SQLite (decimal).
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt <= to)
            .Select(p => new { p.Amount, PlanName = p.Subscription!.Plan!.Name })
            .ToListAsync(ct);

        return new
        {
            totalRevenue = payments.Sum(p => p.Amount),
            payments = payments.Count,
            byPlan = payments.GroupBy(p => p.PlanName)
                .Select(g => new { plan = g.Key, revenue = g.Sum(x => x.Amount), payments = g.Count() })
                .OrderByDescending(x => x.revenue).ToList()
        };
    }

    private static ReportDto ToDto(Report r) => new(
        r.Id, r.Type.ToString(), r.PeriodStart, r.PeriodEnd, r.GeneratedAt, r.GeneratedByUserId,
        JsonSerializer.Deserialize<JsonElement>(r.ResultJson));
}
