using IOApp.Configs;

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
    }
}