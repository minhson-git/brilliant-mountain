using IOCore.Files;
using System.Collections.Generic;

namespace IOApp.Configs
{
    internal class Profile
    {
        public static readonly string[] INPUT_EXTENSIONS;

        static Profile()
        {
            INPUT_EXTENSIONS = ["*"];
        }

        public static readonly Dictionary<ImageFamily.FamilyType, string> PROMOTION_IMAGE_FORMATS = new()
        {
            #region Standard
            { ImageFamily.FamilyType.Ai,   "eps-to" },
            { ImageFamily.FamilyType.Avif, "avif-to" },
            { ImageFamily.FamilyType.Eps,  "eps-to" },
            { ImageFamily.FamilyType.Heic, "heic-to" },
            { ImageFamily.FamilyType.Mpo,  "mpo-to" },
            { ImageFamily.FamilyType.Psd,  "psd-to" },
            { ImageFamily.FamilyType.Qoi,  "qoi-to" },
            { ImageFamily.FamilyType.Sfw,  "sfw-to" },
            { ImageFamily.FamilyType.Svg,  "svg-to" },
            { ImageFamily.FamilyType.Tga,  "tga-to" },
            { ImageFamily.FamilyType.Tiff, "tiff-to" },
            { ImageFamily.FamilyType.Webp, "webp-to" },
            { ImageFamily.FamilyType.Xpm,  "xpm-to" },

            { ImageFamily.FamilyType.Bmp,  "universal-image-converter" },
            { ImageFamily.FamilyType.Gif,  "universal-image-converter" },
            { ImageFamily.FamilyType.Ico,  "icon-maker-studio" },
            { ImageFamily.FamilyType.Jpg,  "universal-image-converter" },
            { ImageFamily.FamilyType.Png,  "universal-image-converter" },

            { ImageFamily.FamilyType.Pbm,  "universal-image-converter" },
            { ImageFamily.FamilyType.Pcx,  "universal-image-converter" },
            { ImageFamily.FamilyType.Wbmp, "universal-image-converter" },
            #endregion

            #region Raw
            { ImageFamily.FamilyType.Arw,  "arw-to" },
            { ImageFamily.FamilyType.Cr2,  "cr2-to" },
            { ImageFamily.FamilyType.Dcr,  "dcr-to" },
            { ImageFamily.FamilyType.Dng,  "dng-to" },
            { ImageFamily.FamilyType.Erf,  "erf-to" },
            { ImageFamily.FamilyType.Mef,  "mef-to" },
            { ImageFamily.FamilyType.Nef,  "nef-to" },
            { ImageFamily.FamilyType.Orf,  "orf-to" },
            { ImageFamily.FamilyType.Pef,  "pef-to" },
            { ImageFamily.FamilyType.Raf,  "raf-to" },
            { ImageFamily.FamilyType.Raw,  "raw-to" },
            { ImageFamily.FamilyType.Rw2,  "rw2-to" },
            #endregion
        };
    }
}