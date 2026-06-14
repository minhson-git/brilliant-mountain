using IOApp.Configs;
using IOCore.Base;
using IOCore.Libs;
using System.ComponentModel;
using System.IO;

namespace IOApp.Features
{
    internal partial class SharedStorage : LocalStorage<SharedStorage>, INotifyPropertyChanged
    {
        SharedStorage() { }

        public AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();

        protected new string _PROFILE_PATH => Path.Combine(IOCore.Core.AppDir.PGet(IOCore.Core.AppDir.Type.LocalFolder), "io-shared");

        readonly string _PASSWORD_ENCRYPTED_KEY = Constants.TOKEN;

        public string AppDir
        {
            set => Set(Name(), value);
        }

        public string EncryptedPassword
        {
            set => Set(Name(), value);
        }

        public bool IsLocked
        {
            set => Set(Name(), value);
        }

        public void Sync(bool forceUnlock)
        {
            if (AppPackageStorage.Exists(nameof(AppPackageStorage.AppDir)) && !string.IsNullOrWhiteSpace(AppLocalStorage.Password))
            {
                AppDir = AppPackageStorage.AppDir;
                EncryptedPassword = AppLocalStorage.EncryptedPassword;
            }

            if (forceUnlock)
                IsLocked = false;
        }
    }
}
