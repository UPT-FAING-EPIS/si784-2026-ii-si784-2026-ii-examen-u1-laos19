using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Data;
using MusicStream.Api.Domain;
using MusicStream.Api.Dtos;
using MusicStream.Api.Services;
using MusicStream.Tests.Support;

namespace MusicStream.Tests.Unit;

public class CatalogAndPlaylistTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly SubscriptionService _subscriptions;
    private readonly CatalogService _catalog;
    private readonly PlaylistService _playlists;
    private readonly User _user;

    public CatalogAndPlaylistTests()
    {
        _subscriptions = new SubscriptionService(_db.Context, _db.Clock);
        _catalog = new CatalogService(_db.Context, _subscriptions, _db.Clock);
        _playlists = new PlaylistService(_db.Context, _subscriptions, _db.Clock);
        _user = new User { Email = "luis@test.pe", FullName = "Luis", PasswordHash = "x" };
        _db.Context.Users.Add(_user);
        _db.Context.SaveChanges();
    }

    [Fact]
    public async Task Search_WithoutFilters_ReturnsAllTypes()
    {
        var result = await _catalog.SearchAsync(null, null, null, 1, 100);

        Assert.Equal(4 + 4 + 16, result.Total);
        Assert.Contains(result.Items, i => i.Type == "artist");
        Assert.Contains(result.Items, i => i.Type == "album");
        Assert.Contains(result.Items, i => i.Type == "song");
    }

    [Fact]
    public async Task Search_ByTermAndType_FiltersSongs()
    {
        var result = await _catalog.SearchAsync("garúa", "song", null, 1, 10);

        var item = Assert.Single(result.Items);
        Assert.Equal("Garúa", item.Title);
    }

    [Fact]
    public async Task Search_ByGenre_AndPaging()
    {
        var result = await _catalog.SearchAsync(null, "song", "Rock", 2, 3);

        Assert.Equal(4, result.Total);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Search_InvalidType_Throws()
    {
        await Assert.ThrowsAsync<ServiceException>(() => _catalog.SearchAsync(null, "video", null, 1, 10));
    }

    [Fact]
    public async Task GetDetail_ResolvesSongAlbumAndArtist()
    {
        var song = await _db.Context.Songs.FirstAsync();
        var album = await _db.Context.Albums.FirstAsync();
        var artist = await _db.Context.Artists.FirstAsync();

        Assert.Equal("song", (await _catalog.GetDetailAsync(song.Id)).Type);
        var albumDetail = await _catalog.GetDetailAsync(album.Id);
        Assert.Equal(4, albumDetail.Album!.Songs.Count);
        var artistDetail = await _catalog.GetDetailAsync(artist.Id);
        Assert.Single(artistDetail.Artist!.Albums);
        await Assert.ThrowsAsync<ServiceException>(() => _catalog.GetDetailAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Play_PremiumSongOnFreePlan_IsForbidden()
    {
        var premium = await _db.Context.Songs.FirstAsync(s => s.IsPremium);

        var ex = await Assert.ThrowsAsync<ServiceException>(() => _catalog.PlayAsync(premium.Id, _user.Id));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Play_PremiumSongOnPremiumPlan_RecordsHistory()
    {
        await _subscriptions.CreateOrRenewAsync(_user.Id, DbSeeder.PremiumMonthlyPlanId, true);
        var premium = await _db.Context.Songs.FirstAsync(s => s.IsPremium);

        var play = await _catalog.PlayAsync(premium.Id, _user.Id);

        Assert.StartsWith("https://", play.StreamUrl);
        Assert.Equal(1, await _db.Context.PlaybackHistory.CountAsync(h => h.UserId == _user.Id));
    }

    [Fact]
    public async Task Admin_CreatesArtistAlbumSong_AndDeletes()
    {
        var artist = await _catalog.CreateArtistAsync(new CreateArtistRequest { Name = "Nuevo", Genre = "Pop" });
        var album = await _catalog.CreateAlbumAsync(new CreateAlbumRequest { Title = "Debut", ReleaseYear = 2025, ArtistId = artist.Id });
        var song = await _catalog.CreateSongAsync(new CreateSongRequest
        {
            Title = "Hit", ArtistId = artist.Id, AlbumId = album.Id, Genre = "Pop", DurationSeconds = 200,
            AudioUrl = "https://cdn.example.com/hit.mp3"
        });

        Assert.Equal("Debut", song.AlbumTitle);
        Assert.Contains("Pop", await _catalog.GetGenresAsync());

        await _catalog.DeleteAsync(song.Id);
        await _catalog.DeleteAsync(album.Id);
        await _catalog.DeleteAsync(artist.Id);
        await Assert.ThrowsAsync<ServiceException>(() => _catalog.DeleteAsync(artist.Id));
    }

    [Fact]
    public async Task CreateSong_RejectsInsecureUrlAndForeignAlbum()
    {
        var artist = await _catalog.CreateArtistAsync(new CreateArtistRequest { Name = "X", Genre = "Pop" });
        var foreignAlbum = await _db.Context.Albums.FirstAsync();

        await Assert.ThrowsAsync<ServiceException>(() => _catalog.CreateSongAsync(new CreateSongRequest
        {
            Title = "A", ArtistId = artist.Id, Genre = "Pop", DurationSeconds = 10, AudioUrl = "ftp://cdn.example.com/a.mp3"
        }));
        await Assert.ThrowsAsync<ServiceException>(() => _catalog.CreateSongAsync(new CreateSongRequest
        {
            Title = "A", ArtistId = artist.Id, AlbumId = foreignAlbum.Id, Genre = "Pop", DurationSeconds = 10, AudioUrl = "https://cdn.example.com/a.mp3"
        }));
    }

    [Fact]
    public async Task Playlist_CreateAddRemoveDelete()
    {
        var songs = await _db.Context.Songs.OrderBy(s => s.Title).Take(2).ToListAsync();

        var playlist = await _playlists.CreateAsync(_user.Id, new CreatePlaylistRequest { Name = "Favoritas", SongIds = { songs[0].Id } });
        Assert.Single(playlist.Songs);

        playlist = await _playlists.AddSongAsync(playlist.Id, songs[1].Id, _user.Id);
        Assert.Equal(2, playlist.Songs.Count);
        await Assert.ThrowsAsync<ServiceException>(() => _playlists.AddSongAsync(playlist.Id, songs[1].Id, _user.Id));

        playlist = await _playlists.RemoveSongAsync(playlist.Id, songs[0].Id, _user.Id);
        Assert.Single(playlist.Songs);

        Assert.Single(await _playlists.GetByUserAsync(_user.Id));
        await _playlists.DeleteAsync(playlist.Id, _user.Id, false);
        Assert.Empty(await _playlists.GetByUserAsync(_user.Id));
    }

    [Fact]
    public async Task Playlist_FreePlanLimit_IsEnforced()
    {
        for (var i = 0; i < 3; i++)
        {
            await _playlists.CreateAsync(_user.Id, new CreatePlaylistRequest { Name = $"Lista {i}" });
        }

        var ex = await Assert.ThrowsAsync<ServiceException>(() => _playlists.CreateAsync(_user.Id, new CreatePlaylistRequest { Name = "Extra" }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Playlist_OtherUserCannotModify()
    {
        var playlist = await _playlists.CreateAsync(_user.Id, new CreatePlaylistRequest { Name = "Privada" });
        var song = await _db.Context.Songs.FirstAsync();

        await Assert.ThrowsAsync<ServiceException>(() => _playlists.AddSongAsync(playlist.Id, song.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<ServiceException>(() => _playlists.DeleteAsync(playlist.Id, Guid.NewGuid(), false));
        await Assert.ThrowsAsync<ServiceException>(() => _playlists.CreateAsync(_user.Id, new CreatePlaylistRequest { Name = "Privada" }));
        await Assert.ThrowsAsync<ServiceException>(() => _playlists.CreateAsync(_user.Id, new CreatePlaylistRequest { Name = "Rota", SongIds = { Guid.NewGuid() } }));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
