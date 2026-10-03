using Microsoft.EntityFrameworkCore;
using MusicStream.Api.Domain;

namespace MusicStream.Api.Data;

/// <summary>Contexto de base de datos de la plataforma de streaming.</summary>
public class MusicStreamDbContext : DbContext
{
    public MusicStreamDbContext(DbContextOptions<MusicStreamDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistSong> PlaylistSongs => Set<PlaylistSong>();
    public DbSet<PlaybackHistory> PlaybackHistory => Set<PlaybackHistory>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Artist>(e =>
        {
            e.ToTable("artists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Name);
            e.Property(x => x.Genre).HasMaxLength(60).IsRequired();
            e.Property(x => x.Bio).HasMaxLength(1000);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Album>(e =>
        {
            e.ToTable("albums");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.CoverUrl).HasMaxLength(500);
            e.HasOne(x => x.Artist).WithMany(a => a.Albums).HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Song>(e =>
        {
            e.ToTable("songs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Title);
            e.Property(x => x.Genre).HasMaxLength(60).IsRequired();
            e.Property(x => x.AudioUrl).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Artist).WithMany(a => a.Songs).HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Album).WithMany(a => a.Songs).HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Plan>(e =>
        {
            e.ToTable("plans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Ignore(x => x.IsFree);
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasOne(x => x.User).WithMany(u => u.Subscriptions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plan).WithMany(p => p.Subscriptions).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasOne(x => x.Subscription).WithMany(s => s.Payments).HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Playlist>(e =>
        {
            e.ToTable("playlists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.User).WithMany(u => u.Playlists).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlaylistSong>(e =>
        {
            e.ToTable("playlist_songs");
            e.HasKey(x => new { x.PlaylistId, x.SongId });
            e.HasOne(x => x.Playlist).WithMany(p => p.Songs).HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Song).WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlaybackHistory>(e =>
        {
            e.ToTable("playback_history");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.PlayedAt });
            e.HasOne(x => x.User).WithMany(u => u.PlaybackHistory).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Song).WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ResultJson).IsRequired();
            e.HasOne(x => x.GeneratedBy).WithMany().HasForeignKey(x => x.GeneratedByUserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
