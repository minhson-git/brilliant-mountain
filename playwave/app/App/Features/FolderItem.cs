using IOCore.Collections;
using IOCore.Types;
using IOCore.Types.Items;

namespace IOApp.Features;

public partial class FolderItem(string path) : BaseItem
{
    public string Path { get; set => SetAndNotify(ref field, value); } = path;

    public ListEx<string> FilePaths { get; } = [];
}

public partial class FolderCheckableItem(FolderItem source, bool isChecked = false) : CheckableItem<FolderItem>(source, isChecked) { }