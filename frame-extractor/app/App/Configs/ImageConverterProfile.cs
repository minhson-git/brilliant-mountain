using IOCore.Files;
using System.Collections.Generic;
using static IOCore.Files.ImageFamily;

namespace IOApp.Configs
{
    internal class ImageConverterProfile
    {
        internal static readonly Dictionary<FamilyType, ImageFamily> _INPUT_IMAGE_FAMILIES = new(
        [
            #region Standard
            ZFile.CreateMap(FamilyType.Ai,   true),
            ZFile.CreateMap(FamilyType.Avif, true),
            ZFile.CreateMap(FamilyType.Eps,  true),
            ZFile.CreateMap(FamilyType.Heic, true),
            ZFile.CreateMap(FamilyType.Mpo,  true),
            ZFile.CreateMap(FamilyType.Psd,  true),
            ZFile.CreateMap(FamilyType.Qoi,  true),
            ZFile.CreateMap(FamilyType.Sfw,  true),
            ZFile.CreateMap(FamilyType.Svg,  true),
            ZFile.CreateMap(FamilyType.Tga,  true),
            ZFile.CreateMap(FamilyType.Tiff, true),
            ZFile.CreateMap(FamilyType.Webp, true),
            ZFile.CreateMap(FamilyType.Xpm,  true),

            ZFile.CreateMap(FamilyType.Bmp,  true),
            ZFile.CreateMap(FamilyType.Gif,  true),
            ZFile.CreateMap(FamilyType.Ico,  true),
            ZFile.CreateMap(FamilyType.Jpg,  true),
            ZFile.CreateMap(FamilyType.Png,  true),

            ZFile.CreateMap(FamilyType.Pbm,  true),
            ZFile.CreateMap(FamilyType.Pcx,  true),
            ZFile.CreateMap(FamilyType.Wbmp, true),
            #endregion

            #region Raw
            ZFile.CreateMap(FamilyType.Arw,  true),
            ZFile.CreateMap(FamilyType.Cr2,  true),
            ZFile.CreateMap(FamilyType.Dcr,  true),
            ZFile.CreateMap(FamilyType.Dng,  true),
            ZFile.CreateMap(FamilyType.Erf,  true),
            ZFile.CreateMap(FamilyType.Mef,  true),
            ZFile.CreateMap(FamilyType.Nef,  true),
            ZFile.CreateMap(FamilyType.Orf,  true),
            ZFile.CreateMap(FamilyType.Pef,  true),
            ZFile.CreateMap(FamilyType.Raf,  true),
            ZFile.CreateMap(FamilyType.Raw,  true),
            ZFile.CreateMap(FamilyType.Rw2,  true),
            #endregion
        ]);

        internal static readonly Dictionary<FamilyType, ImageFamily> _OUTPUT_IMAGE_FAMILIES = new(
        [
            ZFile.CreateMap(FamilyType.Jpg,  true),
            ZFile.CreateMap(FamilyType.Png,  true),
            ZFile.CreateMap(FamilyType.Png8, false),
            ZFile.CreateMap(FamilyType.Gif,  false),
            ZFile.CreateMap(FamilyType.Bmp,  true),
            ZFile.CreateMap(FamilyType.Ico,  false),
            ZFile.CreateMap(FamilyType.Avif, false),
            ZFile.CreateMap(FamilyType.Webp, true),

            ZFile.CreateMap(FamilyType.Pbm,  false),
            ZFile.CreateMap(FamilyType.Pcx,  false),
            ZFile.CreateMap(FamilyType.Wbmp, false),
        ]);
    }
}
