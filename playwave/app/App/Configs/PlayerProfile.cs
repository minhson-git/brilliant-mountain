using IOCore.Files;
using XMedia.Media.PlayerBase;

namespace IOApp.Configs;

internal class PlayerProfile
{
    internal static readonly bool HasPlayer = true;

    internal static void Setup()
    {
        SimpleMediaProfiles.EnableAllInput(true);
        SimpleMediaProfiles.EnableAllOutput(true);

        SimpleMediaProfiles.UpdateAllInputExtra("ll-video-audio-converter");

        foreach (var i in PlayerTypes.SPEEDS)
            PlayerTypes.SPEEDS[i.Key].I2 = (int)i.Key > 2000;
    }
}