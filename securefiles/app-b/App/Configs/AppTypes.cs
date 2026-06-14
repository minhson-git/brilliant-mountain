using IOCore.Gens;
using System;
using System.Collections.Generic;
using static IOCore.Files.MediaFamily;
using static IOMedia.Media.PlayerEx;

namespace IOApp.Configs
{
    public class AppTypes
    {
        public enum FileType
        {
            All,
            Video,
            Audio,
            Image,
            Document,
            Pdf,
        }

        public static readonly Dictionary<FileType, L> FILTERS = new()
        {
            { FileType.All, L.All },
            { FileType.Video, L.Video },
            { FileType.Audio, L.Audio },
            { FileType.Image, L.Image },
        };

        public enum Sort
        {
            Newest,
            Oldest,

            A2Z,
            Z2A
        }

        public static readonly Dictionary<Sort, L> SORTS = new()
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

        public static readonly Dictionary<BuiltInPlaylist, (L, bool)> BUILT_IN_PLAYLISTS = new()
        {
            { BuiltInPlaylist.Favorite, (L.Features_Favorite, true) },
            { BuiltInPlaylist.Restrict, (L.Features_Restrict, true) }
        };

        public static readonly Dictionary<SleepDelayKind, (TimeSpan, L)> SLEEPS = new()
        {
            { SleepDelayKind.Off,       (TimeSpan.Zero,            L.Off) },
            { SleepDelayKind._10,       (TimeSpan.FromMinutes(10), L.Share_10Minutes) },
            { SleepDelayKind._15,       (TimeSpan.FromMinutes(15), L.Share_15Minutes) },
            { SleepDelayKind._20,       (TimeSpan.FromMinutes(20), L.Share_20Minutes) },
            { SleepDelayKind._30,       (TimeSpan.FromMinutes(30), L.Share_30Minutes) },
            { SleepDelayKind._45,       (TimeSpan.FromMinutes(45), L.Share_45Minutes) },
            { SleepDelayKind._60,       (TimeSpan.FromHours(1),    L.Share_1Hour) },
            { SleepDelayKind.Ended,     (TimeSpan.Zero,            L.Share_AfterFinishingTheMedia) },
        };

        public enum ExportType
        {
            Export,
            ExportUnlockedCopy,
            ExportLockedCopy,
        }

        public static readonly Dictionary<ExportType, L> EXPORTS = new()
        {
            { ExportType.Export, L.Features_ExportItemToDestination},
            { ExportType.ExportUnlockedCopy, L.Features_ExportUnlockedCopyItemToDestination},
            { ExportType.ExportLockedCopy, L.Features_ExportLockedCopyItemToDestination},
        };
    }
}