using IOCore.Files;
using System.Collections.Generic;
using static IOCore.Files.MediaFamily;

namespace IOApp.Configs
{
    internal class MediaConverterProfile
    {
        internal static readonly Dictionary<FamilyType, MediaFamily> _INPUT_MEDIA_FAMILIES = new(
        [
            #region Video
            ZFile.CreateMap(FamilyType.V_3gp,  true),
            ZFile.CreateMap(FamilyType.V_Asf,  true),
            ZFile.CreateMap(FamilyType.V_Avi,  true),
            ZFile.CreateMap(FamilyType.V_Dat,  true),
            ZFile.CreateMap(FamilyType.V_Flv,  true),
            ZFile.CreateMap(FamilyType.V_Hevc, true),
            ZFile.CreateMap(FamilyType.V_M2ts, true),
            ZFile.CreateMap(FamilyType.V_M4v,  true),
            ZFile.CreateMap(FamilyType.V_Mkv,  true),
            ZFile.CreateMap(FamilyType.V_Mov,  true),
            ZFile.CreateMap(FamilyType.V_Mp4,  true),
            ZFile.CreateMap(FamilyType.V_Mpeg, true),
            ZFile.CreateMap(FamilyType.V_Mxf,  true),
            ZFile.CreateMap(FamilyType.V_Ogg,  true),
            ZFile.CreateMap(FamilyType.V_Ogv,  true),
            ZFile.CreateMap(FamilyType.V_Rm,   true),
            ZFile.CreateMap(FamilyType.V_Swf,  true),
            ZFile.CreateMap(FamilyType.V_Ts,   true),
            ZFile.CreateMap(FamilyType.V_Vob,  true),
            ZFile.CreateMap(FamilyType.V_Webm, true),
            ZFile.CreateMap(FamilyType.V_Wmv,  true),
            #endregion

            #region Audio
            ZFile.CreateMap(FamilyType.A_Aac,  true),
            ZFile.CreateMap(FamilyType.A_Ac3,  true),
            ZFile.CreateMap(FamilyType.A_Aiff, true),
            ZFile.CreateMap(FamilyType.A_Amr,  true),
            ZFile.CreateMap(FamilyType.A_Au,   true),
            ZFile.CreateMap(FamilyType.A_Caf,  true),
            ZFile.CreateMap(FamilyType.A_Dts,  true),
            ZFile.CreateMap(FamilyType.A_Dsd,  true),
            ZFile.CreateMap(FamilyType.A_Flac, true),
            ZFile.CreateMap(FamilyType.A_M4a,  true),
            ZFile.CreateMap(FamilyType.A_M4b,  true),
            ZFile.CreateMap(FamilyType.A_M4p,  true),
            ZFile.CreateMap(FamilyType.A_M4r,  true),
            ZFile.CreateMap(FamilyType.A_Midi, true),
            ZFile.CreateMap(FamilyType.A_Mlp,  true),
            ZFile.CreateMap(FamilyType.A_Mp2,  true),
            ZFile.CreateMap(FamilyType.A_Mp3,  true),
            ZFile.CreateMap(FamilyType.A_Ogg,  true),
            ZFile.CreateMap(FamilyType.A_Opus, true),
            ZFile.CreateMap(FamilyType.A_Tta,  true),
            ZFile.CreateMap(FamilyType.A_Voc,  true),
            ZFile.CreateMap(FamilyType.A_Wav,  true),
            ZFile.CreateMap(FamilyType.A_Weba, true),
            ZFile.CreateMap(FamilyType.A_Wma,  true),
            ZFile.CreateMap(FamilyType.A_Wv,   true),
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
            ZFile.CreateMap(FamilyType.A_Mp3,  false),
            ZFile.CreateMap(FamilyType.A_Aac,  false),
            ZFile.CreateMap(FamilyType.A_Ogg,  false),
            ZFile.CreateMap(FamilyType.A_Flac, false),
            ZFile.CreateMap(FamilyType.A_M4a,  false),
            ZFile.CreateMap(FamilyType.A_M4b,  false),
            ZFile.CreateMap(FamilyType.A_Caf,  false),
            ZFile.CreateMap(FamilyType.A_Wav,  false),
            ZFile.CreateMap(FamilyType.A_Aiff, false),
            ZFile.CreateMap(FamilyType.A_Wma,  false),
            ZFile.CreateMap(FamilyType.A_Ac3,  false),
            ZFile.CreateMap(FamilyType.A_Mp2,  false),
            #endregion
        ]);
    }
}
