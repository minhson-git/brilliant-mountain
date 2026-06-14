using IOApp.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using static IOCore.Files.MediaFamily;

namespace IOApp.Features
{
    public class PlayerShare
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

        public static string GetSlug(string extension, bool hasVideo)
        {
            if (string.IsNullOrWhiteSpace(extension)) return null;

            extension = extension.ToLowerInvariant();

            var formats = hasVideo ? PlayerProfile.INPUT_VIDEO_FAMILIES : PlayerProfile.INPUT_AUDIO_FAMILIES;

            foreach (var i in formats)
                if (i.Value.Extensions.Contains(extension))
                    return i.Value.Extra as string;

            return null;
        }

        public static string GetInputExtensionsTextByGroupFamily(MediaType type, int length = 14)
        {
            var extensions = type switch
            {
                MediaType.Media => PlayerProfile.INPUT_MEDIA_EXTENSIONS,
                MediaType.Video => PlayerProfile.INPUT_VIDEO_EXTENSIONS,
                MediaType.Audio => PlayerProfile.INPUT_AUDIO_EXTENSIONS,
                _ => []
            };

            var extTexts = new List<string>();
            while (extensions.Length > 0)
            {
                extTexts.Add(string.Join(", ", extensions.Take(length)));
                extensions = extensions.Skip(length).ToArray();
            }

            return string.Join("\n", extTexts);
        }
    }
}