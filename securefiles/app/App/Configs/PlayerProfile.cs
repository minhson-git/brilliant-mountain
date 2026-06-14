using IOCore.Exs;
using IOCore.Files;
using System.Collections.Generic;
using System.Linq;
using static IOCore.Files.MediaFamily;
using static IOMedia.Media.PlayerEx;

namespace IOApp.Configs
{
    internal class PlayerProfile
    {
        internal static readonly Dictionary<FamilyType, MediaFamily> _INPUT_MEDIA_FAMILIES = new(
        [
            #region Video
            ZFile.CreateMap(FamilyType.V_Dat,  true,   "dat-to"),
            ZFile.CreateMap(FamilyType.V_Flv,  true,   "swf-to"),
            ZFile.CreateMap(FamilyType.V_Hevc, true,   "hevc-to"),
            ZFile.CreateMap(FamilyType.V_Mxf,  true,   "mxf-to"),
            ZFile.CreateMap(FamilyType.V_Ogg,  true,   "ogg-to"),
            ZFile.CreateMap(FamilyType.V_Ogv,  true,   "ogg-to"),
            ZFile.CreateMap(FamilyType.V_Rm,   true,   "ll-video-audio-converter"),
            ZFile.CreateMap(FamilyType.V_Swf,  true,   "swf-to"),
            ZFile.CreateMap(FamilyType.V_Ts,   true,   "ts-to"),
            ZFile.CreateMap(FamilyType.V_Vob,  true,   "vob-to"),
            ZFile.CreateMap(FamilyType.V_Webm, true,   "webm-to"),

            ZFile.CreateMap(FamilyType.V_3gp,  true,   "3gp-to"),
            ZFile.CreateMap(FamilyType.V_Asf,  true,   "asf-to"),
            ZFile.CreateMap(FamilyType.V_Avi,  true,   "avi-to"),
            ZFile.CreateMap(FamilyType.V_M2ts, true,   "m2ts-to"),
            ZFile.CreateMap(FamilyType.V_M4v,  true,   "ll-video-audio-converter"),
            ZFile.CreateMap(FamilyType.V_Mkv,  true,   "mkv-to"),
            ZFile.CreateMap(FamilyType.V_Mov,  true,   "mov-to"),
            ZFile.CreateMap(FamilyType.V_Mp4,  true,   "mp4-to"),
            ZFile.CreateMap(FamilyType.V_Mpeg, true,   "mpeg-to"),
            ZFile.CreateMap(FamilyType.V_Wmv,  true,   "wmv-to"),
            #endregion

            #region Audio
            ZFile.CreateMap(FamilyType.A_Ac3,  true,   "ac3-to"),
            ZFile.CreateMap(FamilyType.A_Amr,  true,   "amr-to"),
            ZFile.CreateMap(FamilyType.A_Caf,  true,   "caf-to"),
            ZFile.CreateMap(FamilyType.A_Dts,  true,   "dts-to"),
            ZFile.CreateMap(FamilyType.A_Dsd,  true,   "dsd-to"),
            ZFile.CreateMap(FamilyType.A_M4b,  true,   "m4a-to"),
            ZFile.CreateMap(FamilyType.A_M4p,  true,   "m4a-to"),
            ZFile.CreateMap(FamilyType.A_M4r,  true,   "m4a-to"),
            ZFile.CreateMap(FamilyType.A_Mlp,  true,   "mlp-to"),
            ZFile.CreateMap(FamilyType.A_Midi, true,   "midi-to"),
            ZFile.CreateMap(FamilyType.A_Ogg,  true,   "ogg-to-mp3"),
            ZFile.CreateMap(FamilyType.A_Opus, true,   "opus-to"),
            ZFile.CreateMap(FamilyType.A_Tta,  true,   "ll-video-audio-converter"),
            ZFile.CreateMap(FamilyType.A_Voc,  true,   "voc-to"),
            ZFile.CreateMap(FamilyType.A_Weba, true,   "weba-to"),

            ZFile.CreateMap(FamilyType.A_Aac,  true,   "aac-to"),
            ZFile.CreateMap(FamilyType.A_Aiff, true,   "aiff-to"),
            ZFile.CreateMap(FamilyType.A_Au,   true,   "au-to"),
            ZFile.CreateMap(FamilyType.A_Flac, true,   "flac-to"),
            ZFile.CreateMap(FamilyType.A_M4a,  true,   "m4a-to"),
            ZFile.CreateMap(FamilyType.A_Mp3,  true,   "ll-video-audio-converter"),
            ZFile.CreateMap(FamilyType.A_Mp2,  true,   "mp3-to"),
            ZFile.CreateMap(FamilyType.A_Wav,  true,   "wav-to"),
            ZFile.CreateMap(FamilyType.A_Wma,  true,   "wma-to"),
            ZFile.CreateMap(FamilyType.A_Wv,   true,   "ll-video-audio-converter")
            #endregion
        ]);

        internal static readonly Dictionary<FamilyType, MediaFamily> _OUTPUT_MEDIA_FAMILIES = new(
        [
            #region Video
            ZFile.CreateMap(FamilyType.V_Mp4,  true),
            ZFile.CreateMap(FamilyType.V_Mov,  true),
            ZFile.CreateMap(FamilyType.V_Mkv,  true),
            ZFile.CreateMap(FamilyType.V_Avi,  true),
            ZFile.CreateMap(FamilyType.V_Wmv,  true),
            ZFile.CreateMap(FamilyType.V_Webm, true),
            ZFile.CreateMap(FamilyType.V_Mxf,  true),
            ZFile.CreateMap(FamilyType.V_M4v,  true),
            ZFile.CreateMap(FamilyType.V_Asf,  true),
            ZFile.CreateMap(FamilyType.V_Vob,  true),
            ZFile.CreateMap(FamilyType.V_Ogv,  true),
            ZFile.CreateMap(FamilyType.V_Flv,  true),
            #endregion

            #region Audio
            ZFile.CreateMap(FamilyType.A_Mp3,  true),
            ZFile.CreateMap(FamilyType.A_Aac,  true),
            ZFile.CreateMap(FamilyType.A_Ogg,  true),
            ZFile.CreateMap(FamilyType.A_Flac, true),
            ZFile.CreateMap(FamilyType.A_M4a,  true),
            ZFile.CreateMap(FamilyType.A_M4b,  true),
            ZFile.CreateMap(FamilyType.A_Caf,  true),
            ZFile.CreateMap(FamilyType.A_Wav,  true),
            ZFile.CreateMap(FamilyType.A_Aiff, true),
            ZFile.CreateMap(FamilyType.A_Wma,  true),
            ZFile.CreateMap(FamilyType.A_Ac3,  true),
            ZFile.CreateMap(FamilyType.A_Mp2,  true),
            #endregion
        ]);

        //

        public static readonly Dictionary<FamilyType, MediaFamily> INPUT_MEDIA_FAMILIES;
        public static readonly string[] INPUT_MEDIA_EXTENSIONS;

        public static readonly Dictionary<FamilyType, MediaFamily> INPUT_VIDEO_FAMILIES;
        public static readonly string[] INPUT_VIDEO_EXTENSIONS;

        public static readonly Dictionary<FamilyType, MediaFamily> INPUT_AUDIO_FAMILIES;
        public static readonly string[] INPUT_AUDIO_EXTENSIONS;

        public static readonly Dictionary<FamilyType, MediaFamily> OUTPUT_MEDIA_FAMILIES;
        public static readonly Dictionary<FamilyType, MediaFamily> OUTPUT_VIDEO_FAMILIES;
        public static readonly Dictionary<FamilyType, MediaFamily> OUTPUT_AUDIO_FAMILIES;

        static PlayerProfile()
        {
            INPUT_MEDIA_FAMILIES = new(_INPUT_MEDIA_FAMILIES.Where(i => i.Value.IsSupported));
            INPUT_MEDIA_EXTENSIONS = INPUT_MEDIA_FAMILIES.Select(i => i.Value.Extensions).SelectMany(i => i).Distinct().ToArray();

            INPUT_VIDEO_FAMILIES = new(INPUT_MEDIA_FAMILIES.Where(i => i.Value.Type == ZFile.FileType.Video));
            INPUT_VIDEO_EXTENSIONS = INPUT_VIDEO_FAMILIES.Select(i => i.Value.Extensions).SelectMany(i => i).Distinct().ToArray();

            INPUT_AUDIO_FAMILIES = new(INPUT_MEDIA_FAMILIES.Where(i => i.Value.Type == ZFile.FileType.Audio));
            INPUT_AUDIO_EXTENSIONS = INPUT_AUDIO_FAMILIES.Select(i => i.Value.Extensions).SelectMany(i => i).Distinct().ToArray();

            //

            OUTPUT_MEDIA_FAMILIES = new(_OUTPUT_MEDIA_FAMILIES.Where(i => i.Value.IsSupported));
            OUTPUT_VIDEO_FAMILIES = new(OUTPUT_MEDIA_FAMILIES.Where(i => i.Value.Type == ZFile.FileType.Video));
            OUTPUT_AUDIO_FAMILIES = new(OUTPUT_MEDIA_FAMILIES.Where(i => i.Value.Type == ZFile.FileType.Audio));

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"\"{string.Join("\", \"", INPUT_MEDIA_EXTENSIONS)}\"");
#endif

            SPEEDS.ForEach(i => SPEEDS[i.Key].I2 = (int)i.Key > 2000);
        }
    }
}