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
            ProcessStopped,
        }
    }
}