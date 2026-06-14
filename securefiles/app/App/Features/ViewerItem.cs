using IOImage;
using IOImage.Presenter;

namespace IOApp.Features
{
    public partial class ViewerItem : PresenterItem
    {
        public ViewerItem(string path) : base(new(path))
        {
        }

        public ViewerItem() : this(string.Empty)
        {
        }

        public ViewerItem(ImageInfoBase inputInfo) : base(inputInfo)
        {
        }

        public override bool Equals(object? obj) => this == obj || GetHashCode() == obj?.GetHashCode();

        public override int GetHashCode() => InputInfo.FullName.GetHashCode();
    }
}