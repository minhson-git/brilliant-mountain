using IOCore.Modules.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace IOApp.Features;

[Table("Recent")]
public class RecentEntity : FileEntity
{
    public RecentEntity(string path) : base(path)
    {
    }

    public RecentEntity(FileEntity entity) : base(entity)
    {
    }
}

[Table("Private")]
public class PrivateEntity : FileEntity
{
    public PrivateEntity(string path) : base(path)
    {
    }

    public PrivateEntity(FileEntity entity) : base(entity)
    {
    }
}

[Table("Folders")]
public class FolderEntity : CoreEntity
{
    public string Path { get; set; }
    public bool IsSelected { get; set; }

    public FolderEntity(string path)
    {
        Path = path;

        UpdateTime(EntityTimeType.CreatedAt);
        UpdateTime(EntityTimeType.UpdatedAt);
        UpdateTime(EntityTimeType.LastOpenedAt);
    }

    public FolderEntity(FolderItem item)
    {
        Path = item.Path;
    }
}

[Table("Playlists")]
public class PlaylistEntity(string name) : CoreEntity
{
    public string Name { get; set; } = name;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Remark { get; set; }

    public List<PlaylistFileEntity> Files { get; set; } = [];
}

[Table("PlaylistFiles")]
public class PlaylistFileEntity : FileEntity
{
    public int PlaylistId { get; set; }
    public PlaylistEntity? Playlist { get; set; } = null;

    public PlaylistFileEntity(string path, int playlistId) : base(path)
    {
        PlaylistId = playlistId;
    }

    public PlaylistFileEntity(FileEntity item, int playlistId) : base(item)
    {
        PlaylistId = playlistId;
    }
}

internal partial class AppDbContext(string baseDir) : CoreDbContext(baseDir)
{
    public DbSet<RecentEntity> Recent { get; private set; }
    public DbSet<PrivateEntity> Private { get; private set; }
    public DbSet<FolderEntity> Folders { get; private set; }
    public DbSet<PlaylistEntity> Playlists { get; private set; }
    public DbSet<PlaylistFileEntity> PlaylistFiles { get; private set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PlaylistEntity>().HasMany(e => e.Files).WithOne(e => e.Playlist).HasForeignKey(e => e.PlaylistId).HasPrincipalKey(e => e.Id);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        base.OnConfiguring(options);

        if (!options.IsConfigured)
            options.UseModel(CompiledModels.AppDbContextModel.Instance);
    }

    public override void Verify()
    {
        try
        {
            var result = Database.ExecuteSql($"PRAGMA integrity_check");

            _ = Private.FirstOrDefault();
            _ = Recent.FirstOrDefault();
            _ = Folders.FirstOrDefault();
            _ = Playlists.FirstOrDefault();
            _ = PlaylistFiles.FirstOrDefault();
        }
        catch
        {
            throw;
        }
    }
}