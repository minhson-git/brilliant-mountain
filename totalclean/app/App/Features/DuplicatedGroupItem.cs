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
    public partial class DuplicatedGroupItem : BaseItem
    {
        public readonly string Key;
        public ObservableCollectionEx<FileSystemItem> FileItems { get; } = [];

        public FileSystemItem? FirstFileItem => FileItems.FirstOrDefault();

        bool _isThumbnailLoaded;

        public DuplicatedGroupItem(string key, ListEx<FileSystemItem> fileItems)
        {
            Key = key;
            FileItems.Add(fileItems);
        }

        public BitmapImage? Thumbnail { get; private set; }
        public ZFile.FileType FileType { get; private set; }
        public void ThumbnailEnqueued(LzQueue lazyQueue, uint w = 96, uint h = 96)
        {
            if (!_isThumbnailLoaded)
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

                if (FileType == ZFile.FileType.Unknown)
                    return;

                StorageItemThumbnail? storageItemThumbnail = null;

                if (FileType == ZFile.FileType.Image)
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.PicturesView);
                else if (FileType == ZFile.FileType.Video)
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.VideosView);
                else if (FileType == ZFile.FileType.Audio)
                    storageItemThumbnail = await ExplorerUtils.GetStorageItemThumbnail(path, ThumbnailMode.MusicView);

                if (storageItemThumbnail != null && storageItemThumbnail.Size != 0)
                    AppEx.UI(() =>
                    {
                        Thumbnail = new();
                        Thumbnail.SetSource(storageItemThumbnail);

                        Notify(nameof(Thumbnail), nameof(FileType));
                    });
            }));
        }
    }
}
