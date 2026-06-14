using IOCore.Libs;
using System.ComponentModel;

namespace IOApp.Features
{
    public class AppPackageStorage : PackageStorage<AppPackageStorage>, INotifyPropertyChanged
    {
        public AppPackageStorage() { }

        public string OldAppDir
        {
            get => GetValueOrDefault(Name(), string.Empty);
            set => Set(Name(), value);
        }

        public string AppDir
        {
            get => GetValueOrDefault(Name(), string.Empty);
            set => Set(Name(), value);
        }
    }
}