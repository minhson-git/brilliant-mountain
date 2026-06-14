using IOApp.Features;
using IOCore.Base;
using System.Text;

namespace IOApp.Configs
{
    internal class Constants
    {
        public static AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();

        public static readonly string TOKEN = "275010a649c4d5690f10dc49b9418456";

        public static readonly byte[] SALT = Encoding.UTF8.GetBytes(TOKEN);
        public static readonly int ITERATIONS = 2048;

        public static readonly string URL_IO_HOW_TO_USE_FAQ = $"https://github.com/mountain-brilliant/SecureFiles/blob/main/how-to-use.md";

        public static readonly int TRIAL_LIMIT = 12;

        public static string THUMBNAIL_ENCRYPT_PASSWORD => AppLocalStorage.Password;
    }
}