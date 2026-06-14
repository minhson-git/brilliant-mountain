using IOCore.Base;
using IOCore.Files;

namespace IOApp.Features
{
    public partial class FolderItem : FileItem
    {
        public ObservableCollectionEx<FileSystemItem> FileItems { get; } = [];

        public FolderItem(string path) : base(path)
        {
            _isSelected = true;
        }
    }
}