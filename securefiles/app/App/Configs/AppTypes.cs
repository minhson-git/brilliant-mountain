using IOCore.Gens;
using System.Collections.Generic;

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

        public enum SortType
        {
            Newest,
            Oldest,

            A2Z,
            Z2A
        }

        public static readonly Dictionary<SortType, L> SORTS = new()
        {
            { SortType.Newest, L.SortNewest },
            { SortType.Oldest, L.SortOldest },

            { SortType.A2Z,    L.SortA2Z },
            { SortType.Z2A,    L.SortZ2A },
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