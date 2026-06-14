using IOCore;
using IOCore.Core;
using IOCore.Files;
using IOCore.Utils;
using RoboSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IOApp.Features
{
    internal class RoboCommandHelper
    {
        public static RoboCommand CreateCommand(CopyOptions options, Action<string>? action)
        {
            var command = new RoboCommand
            {
                CopyOptions = options,
                RetryOptions = new() { RetryCount = 1, RetryWaitTime = 1 }
            };

            if (!string.IsNullOrWhiteSpace(options.Source) && PathUtils.IsRootDirectory(options.Source))
                command.SelectionOptions.AddDirectoryExclusion(Path.Combine(options.Source, EnvironmentUtils.SystemVolumeInformation));

            if (!string.IsNullOrWhiteSpace(options.Destination) && PathUtils.IsRootDirectory(options.Destination))
                command.SelectionOptions.AddDirectoryExclusion(Path.Combine(options.Destination, EnvironmentUtils.SystemVolumeInformation));

            command.CopyOptions.MultiThreadedCopiesCount = Math.Max(4, MathUtils.ReduceToPowerOfTwo(Environment.ProcessorCount / 2));

            if (action is not null)
                command.OnCopyProgressChanged += (_, e) => action(Path.GetFileName(e.CurrentFile.Name));

#if DEBUG
            command.OnError += (_, e) => Logger.Log($"RoboCommand OnError: {e.ErrorPath}, {e.Error}");
            command.OnCommandCompleted += (_, e) => Logger.Log($"RoboCommand OnCommandCompleted: {e.TimeSpan.Seconds}");
            command.OnCommandError += (_, e) => Logger.Log($"RoboCommand OnCommandError: {e.Error}");
#endif

            return command;
        }

        public static Task FastDeleteDirectory(string folderPath, bool contentOnly, bool ignoreException = true)
        {
            if (!Directory.Exists(folderPath))
                return Task.CompletedTask;

            var emptyFolderPath = AppDir.LGet(AppDir.Type.TemporaryFolder, FormatUtils.UID);
            FileUtils.CreateDirectoryIfNotExist(emptyFolderPath);

            var command = CreateCommand(new CopyOptions
            {
                Source = emptyFolderPath,
                Destination = folderPath,
                Purge = true,
            }, null);

            return command.Start().ContinueWith(t =>
            {
                FileUtils.Delete(emptyFolderPath);

                if (t.IsCompletedSuccessfully)
                {
                    if (!contentOnly)
                        FileUtils.Delete(folderPath);
                }
                else if (!ignoreException && t.IsFaulted)
                    throw t.Exception?.InnerException ?? t.Exception!;
            });
        }

        public static Task FastCopyFile(string srcFilePath, string dstFolderPath, Action<string>? action = null)
        {
            if (!File.Exists(srcFilePath))
                return Task.CompletedTask;

            var command = CreateCommand(new CopyOptions
            {
                Source = Path.GetDirectoryName(srcFilePath),
                Destination = dstFolderPath,
            }, action);

            command.CopyOptions.AddFileFilter(Path.GetFileName(srcFilePath));

            return command.Start();
        }

        public static Task FastCopyDirectory(string srcFolderPath, string dstFolderPath, bool mirror = false, bool skipEmptyFolders = false, Action<string>? action = null)
        {
            if (!Directory.Exists(srcFolderPath))
                return Task.CompletedTask;

            FileUtils.CreateDirectoryIfNotExist(dstFolderPath);

            var command = CreateCommand(new CopyOptions
            {
                Source = srcFolderPath,
                Destination = dstFolderPath,
                Mirror = mirror,
                CopySubdirectories = true,
                CopySubdirectoriesIncludingEmpty = !skipEmptyFolders,
            }, action);

            return command.Start();
        }

        public static Task FastMoveDirectory(string srcFolderPath, string dstFolderPath, bool skipEmptyFolders = false, Action<string>? action = null)
        {
            if (!Directory.Exists(srcFolderPath))
                return Task.CompletedTask;

            FileUtils.CreateDirectoryIfNotExist(dstFolderPath);

            var command = CreateCommand(new CopyOptions
            {
                Source = srcFolderPath,
                Destination = dstFolderPath,
                MoveFilesAndDirectories = true,
                MoveFiles = true,
                CopySubdirectories = true,
                CopySubdirectoriesIncludingEmpty = !skipEmptyFolders,
            }, action);

            return command.Start();
        }
    }
}