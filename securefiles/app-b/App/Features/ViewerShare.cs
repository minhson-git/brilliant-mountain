using IOApp.Configs;
using IOCore.Files;
using IOCore.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IOApp.Features
{
    public class ViewerShare
    {
        public enum S
        {
            Ready,

            Loading,
            Loaded,
            LoadFailed,

            ProcessInQueue,
            ProcessStart,
            Processing,
            Processed,
            ProcessFailed,
            ProcessPausing,
            ProcessPaused,
            ProcessStopping,
            ProcessStopped,
        }

        public struct Argv
        {
            public string OutputFolderPath;
            public bool ExportToOriginalFolder;
            public bool OverwriteExistingOutputFiles;
            public AppTypes.ExportType ExportType;
        }

        //public static string GetSlug(PrivateFileItem item)
        //{
        //    if (item == null) return null;

        //    if (item.FileType == ZFile.FileType.Image)
        //    {
        //        foreach (var i in Profile.PROMOTION_IMAGE_FORMATS)
        //            if (ImageFamily.IMAGE_FAMILIES[i.Key].Extensions.Contains(item.OriginalInfo.Extension))
        //                return i.Value;
        //    }
        //    else if (ZUtils.Any(item.FileType, ZFile.FileType.Video, ZFile.FileType.Audio))
        //    {
        //        foreach (var i in Profile.PROMOTION_MEDIA_FORMATS)
        //            if (MediaFamily.MEDIA_FAMILIES[i.Key].Extensions.Contains(item.OriginalInfo.Extension))
        //                return i.Value;
        //    }

        //    return null;
        //}

        public static string GetSlug(ViewerItem item)
        {
            if (item == null) return null;

            foreach (var i in Profile.PROMOTION_IMAGE_FORMATS)
                if (ImageFamily.IMAGE_FAMILIES[i.Key].Extensions.Contains(item.InputInfo.Extension))
                    return i.Value;

            return null;
        }

        //public static string GetSlug(PlayerItem item)
        //{
        //    if (item == null) return null;

        //    foreach (var i in Profile.PROMOTION_MEDIA_FORMATS)
        //        if (MediaFamily.MEDIA_FAMILIES[i.Key].Extensions.Contains(item.InputInfo.Extension))
        //            return i.Value;

        //    return null;
        //}

        public static string GetInputExtensionsTextByGroupFamily()
        {
            const int LENGTH = 14;

            var extensions = Profile.INPUT_EXTENSIONS;

            var extTexts = new List<string>();
            while (extensions.Length > 0)
            {
                extTexts.Add(string.Join(", ", extensions.Take(LENGTH)));
                extensions = extensions.Skip(LENGTH).ToArray();
            }

            return string.Join("\n", extTexts);
        }

        //public static string GetSlugByExtension(string extension, bool hasVideo)
        //{
        //    if (string.IsNullOrWhiteSpace(extension)) return null;

        //    extension = extension.ToLowerInvariant();

        //    var format = MediaFamily.MEDIA_FAMILIES.FirstOrDefault(i =>
        //    {
        //        if (hasVideo && i.Value.Type == ZFile.FileType.Video)
        //            return i.Value.Extensions.Contains(extension);
        //        else
        //            return i.Value.Extensions.Contains(extension);
        //    });

        //    if (format.Value == null) return null;

        //    Profile.PROMOTION_MEDIA_FORMATS.TryGetValue(format.Key, out var slug);
        //    return slug;
        //}

        public static bool IsAcceptedInputExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            return Profile.INPUT_EXTENSIONS.Contains(extension);
        }
    }
}