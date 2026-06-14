using IOApp.Configs;
using IOCore.Exs;
using IOCore.Utils;
using System.ComponentModel;

namespace IOApp.Features
{
    internal partial class AppLocalStorage : LocalStorage<AppLocalStorage>, INotifyPropertyChanged
    {
        AppLocalStorage() { }

        public void Reload() => Load();

        public string EncryptedPassword => GetValueOrDefault(nameof(Password), string.Empty, null);

        public string Password
        {
            get
            {
                var value = GetValueOrDefault(Name(), string.Empty, null);
                return string.IsNullOrWhiteSpace(value) ? value : CryptographyUtils.Decrypt(value, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS) ?? string.Empty;
            }
            set => Set(Name(), CryptographyUtils.Encrypt(value, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS), null);
        }
    }
}