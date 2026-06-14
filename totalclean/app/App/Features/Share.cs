using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace IOApp.Features
{
    public class Share
    {
        public enum S
        {
            Ready,

            Loading,
            Loaded,
            LoadFailed,

            Processing,
            Processed,
            ProcessFailed,

            ProcessPausing,
            ProcessPaused,

            ProcessStopping,
            ProcessStopped,
        }

        public static Dictionary<string, List<string>> GetPathMapFromFolders(List<string> folderPaths, CancellationToken? token = null)
        {
            var filePaths = new Dictionary<string, List<string>>();

            foreach (var folderPath in folderPaths)
            {
                token?.ThrowIfCancellationRequested();
                filePaths.Add(folderPath, []);

                foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    token?.ThrowIfCancellationRequested();
                    filePaths[folderPath].Add(file);
                }
            }

            return filePaths;
        }

        public static List<string> GetFilePathsFromFolders(List<string> folderPaths, CancellationToken? token = null)
        {
            var filePaths = new List<string>();

            foreach (var folderPath in folderPaths)
            {
                token?.ThrowIfCancellationRequested();

                foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    token?.ThrowIfCancellationRequested();
                    filePaths.Add(file);
                }
            }

            return filePaths;
        }

        public static List<string> GetDuplicateFilePathsFromFolders(List<string> folderPaths, CancellationToken? token = null)
        {
            var filePaths = GetFilePathsFromFolders(folderPaths, token);
            return [.. filePaths.GroupBy(i => new FileInfo(i).Length).Where(i => i.Count() > 1).SelectMany(i => i)];
        }

        public static List<DuplicatedGroupItem> BuildDuplicateGroups(List<FolderItem> folderItems)
        {
            var fileItems = folderItems.SelectMany(i => i.FileItems).ToList();
            var fileGroups = fileItems.GroupBy(i => i.Key);
            var duplicateGroups = fileGroups.Where(i => i.Count() > 1);

            return [.. duplicateGroups.Select(i => new DuplicatedGroupItem(i.Key, [.. i]))];
        }

        public static List<string> GetScanningFilePathsFromFolders(List<string> folderPaths)
        {
            var filePaths = new List<string>();

            folderPaths.ForEach(folderPath => filePaths.AddRange(Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).OrderByDescending(Path.GetFileName)));
            return [.. filePaths.GroupBy(i => new FileInfo(i).Length).Where(i => i.Count() > 1).SelectMany(i => i)];
        }
    }
}
