using IOCore.Gens;
using IOCore.Helpers;
using System;
using System.Collections.Generic;

namespace IOApp.Configs
{
    internal class AppTypes
    {
        public enum LargeFileSizeType
        {
            _50MB,
            _100MB,
            _200MB,
            _500MB,
            _1GB,
            _2GB,
            _5GB,
        };

        public static readonly Dictionary<LargeFileSizeType, long> LARGE_FILE_SIZES = new()
        {
            { LargeFileSizeType._50MB,  50 * 1024 * 1024 },
            { LargeFileSizeType._100MB, 100 * 1024 * 1024 },
            { LargeFileSizeType._200MB, 200 * 1024 * 1024 },
            { LargeFileSizeType._500MB, 500 * 1024 * 1024 },
            { LargeFileSizeType._1GB,   1 * 1024 * 1024 * 1024 },
            { LargeFileSizeType._2GB,   2L * 1024 * 1024 * 1024 },
            { LargeFileSizeType._5GB,   5L * 1024 * 1024 * 1024 },
        };

        public enum FileType
        {
            All,
            Video,
            Audio,
            Image,
            Document,
            Others,
        }

        public static readonly Dictionary<FileType, L> FILE_TYPE_FILTERS = new()
        {
            { FileType.All,         L.All       },
            { FileType.Video,       L.Video     },
            { FileType.Audio,       L.Audio     },
            { FileType.Image,       L.Image     },
            { FileType.Document,    L.Document  },
            { FileType.Others,      L.Others    },
        };

        public enum CacheDirectoryType
        {
            Browser,
            Development,
            Gaming,
            Mail,
            Media,
            Office,
            System,
            RecycleBin,
        }

        public static readonly Dictionary<CacheDirectoryType, (string, string, string[])> CACHE_DIRECTORIES = new()
        {
            { CacheDirectoryType.System, (R.T(L.SystemCaches), "\uE770",
                [
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Temp"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\CrashDumps")
                ])
            },
            { CacheDirectoryType.Browser, (R.T(L.BrowserCaches), "\uE721",
                [
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\Microsoft\Edge\User Data\Default\Service Worker"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Vivaldi\User Data\Default\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Opera Software\Opera Stable\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Service Worker"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data\Default\Cache")
                ])
            },
            { CacheDirectoryType.Office, (R.T(L.OfficeCaches), "\uE75A",
                [
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Teams\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft VisualStudio\Caches"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Skype for Desktop\Cache")
                ])
            },
            { CacheDirectoryType.Mail, (R.T(L.MailCaches), "\uE715",
                [
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Mailbird\Temp"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Mailbird\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\eM Client\Temp"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\eM Client\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Windows Live Mail\Temp"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Windows Live Mail\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Outlook\Temp")
                ])
            },
            { CacheDirectoryType.Gaming, (R.T(L.GamingCaches), "\uE7FC",
                [
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\NVIDIA\DXCache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\NVIDIA\GLCache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Steam\htmlcache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\EpicGamesLauncher\Saved\Logs")
                ])
            },
            { CacheDirectoryType.Media, (R.T(L.MediaSoftwareCaches), "\uEA69",
                [
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Spotify\Data"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Adobe\Premiere Pro\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Adobe\After Effects\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Apple Computer\iTunes\Cache")
                ])
            },
            { CacheDirectoryType.Development, (R.T(L.DevelopmentToolsCaches), "\uEC7A",
                [
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\npm-cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\JetBrains\Cache"),
                    Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft VisualStudio\Caches")
                ])
            },
            { CacheDirectoryType.RecycleBin, (R.T(L.RecycleBin), "\uE74D", []) }
        };
    }
}
