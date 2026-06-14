using IOApp.Configs;
using IOCore.Libs;
using System;
using System.Drawing;
using System.Linq;

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

        public static bool IsAcceptedInputExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            return Profile.INPUT_EXTENSIONS.Contains(extension);
        }
    }

    public class IOSize<T> where T : IComparable<T>
    {
        public T W;
        public T H;

        public IOSize()
        {
        }

        public IOSize(T w, T h)
        {
            W = w;
            H = h;
        }

        public void Set(T w, T h)
        {
            W = w;
            H = h;
        }

        public void Transpose() => (W, H) = (H, W);

        public Size Drawing
        {
            get
            {
                var cast = Operator.Cast<T, int>();
                return new(cast(W), cast(H));
            }
        }
    }

}