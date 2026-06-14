using IOCore;
using IOCore.Base;
using IOCore.Files;
using IOCore.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using Windows.Storage.FileProperties;

namespace IOApp.Features
{
    public partial class LargeFileGroupItem : BaseItem
    {
        public readonly string Key;
        public readonly string GroupName;
        public ObservableCollectionEx<FileSystemItem> FileItems { get; } = [];

        public FileSystemItem? FirstFileItem => FileItems.FirstOrDefault();
        public FileSystemItem? LargestFileItem => FileItems.OrderByDescending(f => f.InputInfo.FileSize).FirstOrDefault();

        public int FileCount => FileItems.Count;
        public long TotalSize => FileItems.Sum(f => f.InputInfo.FileSize);
        public string TotalSizeText => FileUtils.GetReadableByteSizeText(TotalSize);

        bool _isThumbnailLoaded;

        public LargeFileGroupItem(string key, string groupName, ListEx<FileSystemItem> fileItems)
        {
            Key = key;
            GroupName = groupName;
            FileItems.Add(fileItems);
        }

        public BitmapImage? Thumbnail { get; private set; }
        public ZFile.FileType FileType { get; private set; }

        public void ThumbnailEnqueued(LzQueue lazyQueue, uint w = 96, uint h = 96)
        {
            if (_isThumbnailLoaded)
                return;

            _isThumbnailLoaded = true;

            var path = FirstFileItem?.InputInfo?.FullName;
            if (string.IsNullOrWhiteSpace(path))
                return;

            lazyQueue.Enqueue(new LzAction<bool>(true, async _ =>
            {
                var mimeType = await ExplorerUtils.GetMimeType(path);
                if (mimeType == null)
                    return;

                if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    FileType = ZFile.FileType.Image;
                else if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                    FileType = ZFile.FileType.Video;
                else if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                    FileType = ZFile.FileType.Audio;
                else
                    FileType = ZFile.FileType.Unknown;

                StorageItemThumbnail? storageItemThumbnail = null;

                if (FileType == ZFile.FileType.Image)
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.PicturesView);
                else if (FileType == ZFile.FileType.Video)
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.VideosView);
                else if (FileType == ZFile.FileType.Audio)
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.MusicView);
                else
                {
                    // For other file types, try to get a generic thumbnail
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.DocumentsView);
                }

                if (storageItemThumbnail != null && storageItemThumbnail.Size != 0)
                {
                    AppEx.UI(() =>
                    {
                        Thumbnail = new();
                        Thumbnail.SetSource(storageItemThumbnail);

                        Notify(nameof(Thumbnail), nameof(FileType));
                    });
                }
                else
                {
                    // If no thumbnail available, just notify FileType change
                    AppEx.UI(() => Notify(nameof(FileType)));
                }
            }));
        }
    }
}