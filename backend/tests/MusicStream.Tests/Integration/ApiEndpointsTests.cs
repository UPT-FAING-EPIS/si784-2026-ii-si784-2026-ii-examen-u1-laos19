using System.Net;
using System.Net.Http.Json;
using MusicStream.Api.Data;
using MusicStream.Api.Dtos;

namespace MusicStream.Tests.Integration;

public class ApiEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_IsHealthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMusic_IsPublic_AndSupportsSearch()
    {
        var client = _factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<CatalogItemDto>>("/music?search=lima&pageSize=50");

        Assert.NotNull(page);
        Assert.Contains(page!.Items, i => i.Title == "Medianoche en Lima");
    }

    [Fact]
    public async Task GetMusicById_ReturnsDetail_Or404()
    {
        var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<PagedResult<CatalogItemDto>>("/music?type=artist");
        var artistId = page!.Items[0].Id;

        var detail = await client.GetFromJsonAsync<MusicDetailDto>($"/music/{artistId}");
        Assert.Equal("artist", detail!.Type);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/music/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Register_InvalidPayload_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register", new { email = "no-es-correo", fullName = "A", password = "corta" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = ApiFactory.AdminEmail, password = "incorrecta" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubscriptionLifecycle_CreateGetCancel()
    {
        var (client, auth) = await _factory.CreateUserClientAsync();

        var free = await client.GetFromJsonAsync<SubscriptionDto>($"/subscriptions/{auth.User.Id}");
        Assert.Equal("Free", free!.PlanName);

        var create = await client.PostAsJsonAsync("/subscriptions", new { planId = DbSeeder.PremiumMonthlyPlanId, autoRenew = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var premium = await create.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.Equal("Premium Mensual", premium!.PlanName);

        var cancel = await client.DeleteAsync($"/subscriptions/{premium.Id}");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/subscriptions/{auth.User.Id}")).StatusCode);
    }

    [Fact]
    public async Task Subscriptions_RequireAuth_AndOwnership()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/subscriptions/{Guid.NewGuid()}")).StatusCode);

        var (client, _) = await _factory.CreateUserClientAsync();
        var (_, other) = await _factory.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/subscriptions/{other.User.Id}")).StatusCode);

        var missingPlan = await client.PostAsJsonAsync("/subscriptions", new { autoRenew = true });
        Assert.Equal(HttpStatusCode.BadRequest, missingPlan.StatusCode);
    }

    [Fact]
    public async Task Playlists_CreateAndList_PlayAndRecommend()
    {
        var (client, auth) = await _factory.CreateUserClientAsync();
        var songs = await client.GetFromJsonAsync<PagedResult<CatalogItemDto>>("/music?type=song&pageSize=100");
        var freeSong = songs!.Items.First(s => !s.IsPremium);
        var premiumSong = songs.Items.First(s => s.IsPremium);

        var create = await client.PostAsJsonAsync("/playlists", new { name = "Ruta", songIds = new[] { freeSong.Id } });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var playlists = await client.GetFromJsonAsync<List<PlaylistDto>>($"/playlists/{auth.User.Id}");
        Assert.Single(playlists!);

        var invalid = await client.PostAsJsonAsync("/playlists", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var play = await client.PostAsync($"/music/{freeSong.Id}/play", null);
        Assert.Equal(HttpStatusCode.OK, play.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/music/{premiumSong.Id}/play", null)).StatusCode);

        var history = await client.GetFromJsonAsync<List<HistoryItemDto>>($"/history/{auth.User.Id}");
        Assert.Single(history!);
        var recs = await client.GetFromJsonAsync<List<RecommendationDto>>($"/recommendations/{auth.User.Id}");
        Assert.NotEmpty(recs!);
    }

    [Fact]
    public async Task Reports_OnlyForAdmins()
    {
        var (userClient, _) = await _factory.CreateUserClientAsync();
        var body = new { type = "Revenue", from = DateTime.UtcNow.AddDays(-7), to = DateTime.UtcNow.AddDays(1) };
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PostAsJsonAsync("/reports", body)).StatusCode);

        var (admin, _) = await _factory.CreateAdminClientAsync();
        var created = await admin.PostAsJsonAsync("/reports", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/reports", new { type = "Otro", from = DateTime.UtcNow, to = DateTime.UtcNow })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/subscriptions?status=Active")).StatusCode);
    }

    [Fact]
    public async Task Admin_ManagesPlansAndCatalog()
    {
        var (admin, _) = await _factory.CreateAdminClientAsync();

        var plan = await admin.PostAsJsonAsync("/plans", new { name = $"Plan {Guid.NewGuid():N}", description = "x", price = 5, durationDays = 7, maxPlaylists = 5, allowsPremiumContent = true });
        Assert.Equal(HttpStatusCode.Created, plan.StatusCode);
        var invalidPlan = await admin.PostAsJsonAsync("/plans", new { name = "", price = -1, durationDays = 0, maxPlaylists = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPlan.StatusCode);

        var artist = await admin.PostAsJsonAsync("/music/artists", new { name = "Integración", genre = "Pop" });
        Assert.Equal(HttpStatusCode.Created, artist.StatusCode);
        var created = await artist.Content.ReadFromJsonAsync<ArtistDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/music/{created!.Id}")).StatusCode);

        var plans = await _factory.CreateClient().GetFromJsonAsync<List<PlanDto>>("/plans");
        Assert.True(plans!.Count >= 3);
    }
}
