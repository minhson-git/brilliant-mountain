namespace IOApp.Features
{
    internal class Share
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
    }
} 