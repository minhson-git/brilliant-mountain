using IOMedia.Media;

namespace IOApp.Features
{
    public partial class PlayerItem(MediaInfoBase inputInfo) : MediaPlayerItem(inputInfo)
    {
        public override bool Equals(object? obj) => this == obj || GetHashCode() == obj?.GetHashCode();

        public override int GetHashCode() => InputInfo.FullName.GetHashCode();
    }
}