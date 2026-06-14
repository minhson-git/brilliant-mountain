using IOCore.Collections;
using IOCore.Types.Items;
using IOCore.Utils;
using System;
using System.Linq;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features;

public partial class PlaylistItem : BaseItem
{
    public int Id { get; set => SetAndNotify(ref field, value); }
    public string Name { get; set => SetAndNotify(ref field, value); }
    public string? Description { get; set => SetAndNotify(ref field, value); }
    public string? Remark { get; set => SetAndNotify(ref field, value); }

    public ListEx<string> Paths { get; } = [];

    public BuiltInPlaylist PlaylistType => Enum.TryParse(Remark, out BuiltInPlaylist result) ? result : default;

    public bool IsBuiltIn => BUILT_IN_PLAYLISTS.Select(i => i.Key.ToString()).Contains(Remark);

    public string Icon => Remark switch
    {
        null => "\uE90B",
        var remark when remark is nameof(BuiltInPlaylist.Favorite) => "\uEB52",
        var remark when remark is nameof(BuiltInPlaylist.Restrict) => "\uEA18",
        _ => string.Empty
    };

    public PlaylistItem(string name)
    {
        Name = name;
        IsSelected = true;
    }

    public PlaylistItem(PlaylistEntity entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Description = entity.Description;
        Order = entity.Order;
        Remark = entity.Remark;

        entity.Files.Select(i => i.Path).Let(Paths.ReplaceRange);
        Notify(nameof(Paths));
    }

    public override bool Equals(object? obj) => obj is PlaylistItem item && item.Id == Id;
    public override int GetHashCode() => Id;
}