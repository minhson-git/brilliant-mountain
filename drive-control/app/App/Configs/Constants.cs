using IOCore.Files;

namespace IOApp.Configs
{
    internal class Constants
    {
        public const long LIMIT = 512 * 1024 * 1024;
        public static string LIMIT_TEXT => FileUtils.GetReadableByteSizeText(LIMIT);

        public const int TRIAL_FILES_REMOVE_LIMIT = 20;
        public static string TRIAL_FILES_REMOVE_LIMIT_TEXT => "";
    }
}