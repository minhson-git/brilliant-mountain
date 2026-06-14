using IOCore;
using IOCore.Annotation;
using System.Threading.Tasks;

namespace IOApp.Features;

public class PlayerContext : Singleton<PlayerContext>
{
    public static async Task<bool> AddVideoFile(WindowEx window, string? path)
    {
        //if (path == null)
        //    await Picker.OpenSingleFile(
        //        picker => PlayerConfig.I.InputVideoExtensions.ForEach(i => picker.FileTypeFilter.Add(i)),
        //        storageFile => path = storageFile.Path);

        //if (path is null)
        //    return false;

        //window.Navigate(typeof(Home), null).Let(page => page.LoadVideo(path));

        return true;
    }

    public static async Task<bool> AddAudioFile(WindowEx window, string? path)
    {
        //if (path == null)
        //    await Picker.OpenSingleFile(
        //        picker => PlayerConfig.I.InputAudioExtensions.ForEach(i => picker.FileTypeFilter.Add(i)),
        //        storageFile => path = storageFile.Path);

        //if (path is null)
        //    return false;

        //window.Navigate<Home>(null).Let(page => page.LoadAudio(path));

        return true;
    }
}