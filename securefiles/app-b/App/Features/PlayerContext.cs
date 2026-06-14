using IOApp.Dialogs;
using IOCore.Base;
using IOCore.Files;
using IOCore.Libs;
using IOCore.Utils;
using IOMedia.Media;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features
{
    public partial class PlayerItem : MediaPlayerItem
    {
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();

        public PlayerItem(MediaInfoBase inputInfo) : base(inputInfo)
        {
            InputInfo.PropertyChanged += (_, _) =>
                Notify(nameof(AppPackageStorage));
        }

        internal static PlayerItem Create(SecuredFileEntity entity, bool isRecent, bool ignoreError = false)
        {
            if (!FileUtils.IsFile(entity.Path) && !ignoreError)
                throw new FileNotFoundException();

            return new(MediaInfoBase.Create(entity.Path)) { IsRecent = isRecent, LastOpenedAt = entity.LastOpenedAt };
        }

        public override bool Equals(object? obj) => this == obj || GetHashCode() == obj?.GetHashCode();

        public override int GetHashCode() => InputInfo.FullName.GetHashCode();
    }
}