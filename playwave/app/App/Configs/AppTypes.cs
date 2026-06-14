using IOApp.Gens;
using System.Collections.Generic;
using static IOCore.Files.MediaFamily;

namespace IOApp.Configs;

public class AppTypes
{
    public static readonly IReadOnlyDictionary<MediaType, L> MEDIAS = new Dictionary<MediaType, L>()
    {
        { MediaType.Media, L.All },
        { MediaType.Video, L.Video },
        { MediaType.Audio, L.Audio },
    };

    public enum Sort
    {
        Newest,
        Oldest,

        A2Z,
        Z2A
    }

    public static readonly IReadOnlyDictionary<Sort, L> SORTS = new Dictionary<Sort, L>()
    {
        { Sort.Newest, L.SortNewest },
        { Sort.Oldest, L.SortOldest },

        { Sort.A2Z,    L.SortA2Z },
        { Sort.Z2A,    L.SortZ2A },
    };

    public enum BuiltInPlaylist
    {
        Restrict = -2,
        Favorite,
        Normal,
    }

    public static readonly IReadOnlyDictionary<BuiltInPlaylist, (L, bool)> BUILT_IN_PLAYLISTS = new Dictionary<BuiltInPlaylist, (L, bool)>()
    {
        { BuiltInPlaylist.Favorite, (L.Favorite, true) },
        { BuiltInPlaylist.Restrict, (L.Restrict, true) }
    };
}