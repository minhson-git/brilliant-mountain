using IOApp.Pages;
using IOCore;
using IOCore.Base;
using IOCore.Libs;
using IOCore.Utils;
using IOMedia.Media;
using System.Threading.Tasks;

namespace IOApp.Features
{
    public class PlayerContext : Singleton<PlayerContext>
    {
        public static async Task<bool> AddVideoFile(WindowEx window, string? path)
        {
            if (path == null)
                await window.Picker.OpenSingleFile(
                    picker => EncapsulatedSingleton<PlayerConfig>.ExposeInstance().InputVideoExtensions.ForEach(i => picker.FileTypeFilter.Add(i)),
                    storageFile => path = storageFile.Path);

            if (path.IsNullOrEmpty())
                return false;

            window.Navigate<Home>(null).Let(page => page.LoadVideo(path));

            return true;
        }

        public static async Task<bool> AddAudioFile(WindowEx window, string? path)
        {
            if (path == null)
                await window.Picker.OpenSingleFile(
                    picker => EncapsulatedSingleton<PlayerConfig>.ExposeInstance().InputAudioExtensions.ForEach(i => picker.FileTypeFilter.Add(i)),
                    storageFile => path = storageFile.Path);

            if (path.IsNullOrEmpty())
                return false;

            window.Navigate<Home>(null).Let(page => page.LoadAudio(path));

            return true;
        }
    }
}