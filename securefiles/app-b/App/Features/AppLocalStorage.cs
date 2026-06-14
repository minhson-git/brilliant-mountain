using IOApp.Configs;
using IOCore.Libs;
using IOCore.Utils;
using System.ComponentModel;

namespace IOApp.Features
{
    internal class AppLocalStorage : LocalStorage<AppLocalStorage>, INotifyPropertyChanged
    {
        AppLocalStorage() { }

        public string EncryptedPassword
        {
            get
            {
                var value = GetValueOrDefault(nameof(Password), string.Empty);
                return value;
            }
        }

        public string Password
        {
            get
            {
                var name = Name();
                var value = GetValueOrDefault(name, string.Empty);
                return string.IsNullOrWhiteSpace(value) ? value : CryptographyUtils.Decrypt(value, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS);
            }
            set
            {
                var name = Name();
                Set(name, CryptographyUtils.Encrypt(value, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS));
            }
        }
    }
}