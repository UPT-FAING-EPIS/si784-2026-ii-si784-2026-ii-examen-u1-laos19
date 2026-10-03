using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;
using MusicStream.Api.Services;
using MusicStream.Tests.Support;

namespace MusicStream.Tests.Unit;

public class RecommendationAndReportTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly User _user;
    private readonly User _admin;

    public RecommendationAndReportTests()
    {
        _user = new User { Email = "eva@test.pe", FullName = "Eva", PasswordHash = "x" };
        _admin = new User { Email = "admin@test.pe", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin };
        _db.Context.Users.AddRange(_user, _admin);
        _db.Context.SaveChanges();
    }

    [Fact]
    public async Task Recommendations_PrioritizeListenedArtistAndExcludePlayed()
    {
        var jazzSong = await _db.Context.Songs.Include(s => s.Artist).FirstAsync(s => s.Genre == "Jazz");
        _db.Context.PlaybackHistory.Add(new PlaybackHistory { UserId = _user.Id, SongId = jazzSong.Id, PlayedAt = _db.Clock.GetUtcNow().UtcDateTime });
        await _db.Context.SaveChangesAsync();
        var sut = new RecommendationService(_db.Context);

        var recs = await sut.GetRecommendationsAsync(_user.Id, 5);

        Assert.DoesNotContain(recs, r => r.Song.Id == jazzSong.Id);
        Assert.Equal("Jazz", recs[0].Song.Genre);
        Assert.StartsWith("Porque escuchas a", recs[0].Reason);
        var history = await sut.GetHistoryAsync(_user.Id, 10);
        Assert.Single(history);
    }

    [Fact]
    public async Task Recommendations_ColdStart_ReturnsPopular()
    {
        var sut = new RecommendationService(_db.Context);
        var recs = await sut.GetRecommendationsAsync(_user.Id, 3);

        Assert.Equal(3, recs.Count);
        Assert.All(recs, r => Assert.Equal("Popular en MusicStream", r.Reason));
    }

    [Theory]
    [InlineData("Usage", "totalPlays")]
    [InlineData("Subscriptions", "activeNow")]
    [InlineData("Revenue", "totalRevenue")]
    public async Task Generate_EachType_PersistsReport(string type, string expectedKey)
    {
        var subs = new SubscriptionService(_db.Context, _db.Clock);
        await subs.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, true);
        var song = await _db.Context.Songs.FirstAsync();
        await new CatalogService(_db.Context, subs, _db.Clock).PlayAsync(song.Id, _user.Id);
        var sut = new ReportService(_db.Context, _db.Clock);
        var now = _db.Clock.GetUtcNow().UtcDateTime;

        var report = await sut.GenerateAsync(new GenerateReportRequest { Type = type, From = now.AddDays(-1), To = now.AddDays(1) }, _admin.Id);

        var json = JsonSerializer.Serialize(report.Result);
        Assert.Contains(expectedKey, json);
        Assert.Single(await sut.ListAsync());
    }

    [Fact]
    public async Task Generate_InvalidPeriod_Throws()
    {
        var sut = new ReportService(_db.Context, _db.Clock);
        var now = _db.Clock.GetUtcNow().UtcDateTime;

        await Assert.ThrowsAsync<ServiceException>(() => sut.GenerateAsync(new GenerateReportRequest { Type = "Usage", From = now, To = now.AddDays(-1) }, _admin.Id));
        await Assert.ThrowsAsync<ServiceException>(() => sut.GenerateAsync(new GenerateReportRequest { Type = "Usage", From = now.AddYears(-2), To = now }, _admin.Id));
    }

    [Fact]
    public async Task Plans_CreateUpdateDeactivate()
    {
        var sut = new PlanService(_db.Context);
        var created = await sut.CreateAsync(new UpsertPlanRequest { Name = "Estudiante", Description = "Descuento", Price = 9.904m, DurationDays = 30, MaxPlaylists = 50, AllowsPremiumContent = true });
        Assert.Equal(9.90m, created.Price);

        await Assert.ThrowsAsync<ServiceException>(() => sut.CreateAsync(new UpsertPlanRequest { Name = "Estudiante", DurationDays = 1, MaxPlaylists = 1 }));
        var updated = await sut.UpdateAsync(created.Id, new UpsertPlanRequest { Name = "Estudiante Plus", Price = 12m, DurationDays = 30, MaxPlaylists = 50 });
        Assert.Equal("Estudiante Plus", updated.Name);

        await sut.DeactivateAsync(created.Id);
        Assert.DoesNotContain(await sut.ListAsync(false), p => p.Id == created.Id);
        Assert.Contains(await sut.ListAsync(true), p => p.Id == created.Id);
        await Assert.ThrowsAsync<ServiceException>(() => sut.DeactivateAsync(DbSeeder.FreePlanId));
        await Assert.ThrowsAsync<ServiceException>(() => sut.UpdateAsync(Guid.NewGuid(), new UpsertPlanRequest { Name = "N", DurationDays = 1, MaxPlaylists = 1 }));
    }

    [Fact]
    public async Task Users_AdminCanUpdateOthersButNotSelf()
    {
        var sut = new UserAdminService(_db.Context);

        var updated = await sut.UpdateAsync(_user.Id, new UpdateUserRequest { IsActive = false, Role = "Admin" }, _admin.Id);
        Assert.False(updated.IsActive);
        Assert.Equal("Admin", updated.Role);
        await Assert.ThrowsAsync<ServiceException>(() => sut.UpdateAsync(_admin.Id, new UpdateUserRequest { IsActive = false }, _admin.Id));
        Assert.Single(await sut.ListAsync("eva"));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
