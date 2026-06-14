using IOCore.Base;

namespace IOApp.Features
{
    public partial class FolderItem(string path) : BaseItem
    {
        string _path = path;
        public string Path { get => _path; set => SetAndNotify(ref _path, value); }

        public ListEx<string> FilePaths { get; } = [];
    }
}