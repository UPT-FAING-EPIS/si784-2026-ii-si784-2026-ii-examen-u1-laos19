using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Services;
using MusicStream.Tests.Support;

namespace MusicStream.Tests.Unit;

public class SubscriptionServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly SubscriptionService _sut;
    private readonly User _user;

    public SubscriptionServiceTests()
    {
        _sut = new SubscriptionService(_db.Context, _db.Clock);
        _user = new User { Email = "ana@test.pe", FullName = "Ana", PasswordHash = "x", CreatedAt = _db.Clock.GetUtcNow().UtcDateTime };
        _db.Context.Users.Add(_user);
        _db.Context.SaveChanges();
    }

    [Fact]
    public async Task CreateOrRenew_NewPaidPlan_CreatesActiveSubscriptionAndPayment()
    {
        var result = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, autoRenew: true);

        Assert.Equal("Active", result.Status);
        Assert.Equal(_db.Clock.GetUtcNow().UtcDateTime.AddDays(30), result.EndDate);
        Assert.Equal(1, await _db.Context.Payments.CountAsync());
    }

    [Fact]
    public async Task CreateOrRenew_SamePlan_ExtendsEndDate()
    {
        var first = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, true);
        var renewed = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, true);

        Assert.Equal(first.Id, renewed.Id);
        Assert.Equal(first.EndDate.AddDays(30), renewed.EndDate);
        Assert.Equal(2, await _db.Context.Payments.CountAsync());
    }

    [Fact]
    public async Task CreateOrRenew_DifferentPlan_CancelsPrevious()
    {
        var free = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.FreePlanId, true);
        var premium = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumYearlyPlanId, false);

        var previous = await _db.Context.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == free.Id);
        Assert.Equal(SubscriptionStatus.Cancelled, previous.Status);
        Assert.Equal("Premium Anual", premium.PlanName);
        Assert.Equal(0, await _db.Context.Payments.CountAsync(p => p.SubscriptionId == free.Id));
    }

    [Fact]
    public async Task CreateOrRenew_InactivePlan_Throws()
    {
        var plan = await _db.Context.Plans.SingleAsync(p => p.Id == DbSeeder.PremiumYearlyPlanId);
        plan.IsActive = false;
        await _db.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ServiceException>(() => _sut.CreateOrRenewAsync(_user.Id, plan.Id, true));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateOrRenew_UnknownUser_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<ServiceException>(() => _sut.CreateOrRenewAsync(Guid.NewGuid(), DbSeeder.FreePlanId, true));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetActive_AfterExpirationWithoutAutoRenew_ReturnsNull()
    {
        await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, autoRenew: false);
        _db.Clock.Advance(TimeSpan.FromDays(31));

        Assert.Null(await _sut.GetActiveAsync(_user.Id));
    }

    [Fact]
    public async Task GetActive_AfterExpirationWithAutoRenew_RenewsAndCharges()
    {
        await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, autoRenew: true);
        _db.Clock.Advance(TimeSpan.FromDays(31));

        var active = await _sut.GetActiveAsync(_user.Id);

        Assert.NotNull(active);
        Assert.True(active!.EndDate > _db.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal(2, await _db.Context.Payments.CountAsync());
    }

    [Fact]
    public async Task Cancel_ByOtherUser_IsForbidden()
    {
        var sub = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.FreePlanId, true);

        var ex = await Assert.ThrowsAsync<ServiceException>(() => _sut.CancelAsync(sub.Id, Guid.NewGuid(), false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Cancel_Twice_Conflicts()
    {
        var sub = await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.FreePlanId, true);
        var cancelled = await _sut.CancelAsync(sub.Id, _user.Id, false);

        Assert.Equal("Cancelled", cancelled.Status);
        var ex = await Assert.ThrowsAsync<ServiceException>(() => _sut.CancelAsync(sub.Id, _user.Id, false));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task GetEffectivePlan_WithoutSubscription_IsFree()
    {
        var plan = await _sut.GetEffectivePlanAsync(_user.Id);
        Assert.Equal(DbSeeder.FreePlanId, plan.Id);
    }

    [Fact]
    public async Task List_FiltersByStatus_AndRejectsInvalid()
    {
        await _sut.CreateOrRenewAsync(_user.Id, DbSeeder.FreePlanId, true);

        Assert.Single(await _sut.ListAsync("active"));
        Assert.Empty(await _sut.ListAsync("Cancelled"));
        await Assert.ThrowsAsync<ServiceException>(() => _sut.ListAsync("bogus"));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
