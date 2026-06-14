using IOCore.Exs;
using System.ComponentModel;

namespace IOApp.Features
{
    public partial class AppPackageStorage : PackageStorage<AppPackageStorage>, INotifyPropertyChanged
    {
        AppPackageStorage() { }

        public string OldAppDir
        {
            get => GetValueOrDefault(Name(), string.Empty, null);
            set => Set(Name(), value, null);
        }

        public string AppDir
        {
            get => GetValueOrDefault(Name(), string.Empty, null);
            set => Set(Name(), value, null);
        }
    }
}