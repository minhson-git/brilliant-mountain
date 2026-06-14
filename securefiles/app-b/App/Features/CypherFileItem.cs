using IOApp.Configs;
using IOCore;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Libs;
using IOCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using IOCore.Core;
using IOCore.Helpers;
using IOCore.Base;




#if USE_THUMBNAIL
using ImageMagick;
using IOImage;
using IOMedia.Media;
#endif

namespace IOApp.Features
{
    public partial class CypherFileItem : FileItem
    {
        public enum CypherKind
        {
            Normal,
            EncodedFile,
            EncodedFolder
        };

        public CypherKind Kind => Footer?.Metadata != null ? Footer.Metadata.IsFile ? CypherKind.EncodedFile : CypherKind.EncodedFolder : CypherKind.Normal;

        public FileFooter Footer { get; protected set; }

        public FileInfoBase EncryptedInfo { get; set; }
        public FileInfoBase OriginalInfo { get; set; }

        public bool IsCorrupted => Footer == null;

        public CypherFileItem(string path, bool loadFooter = false) : base(path)
        {
            if (loadFooter) TryLoadFooter(false);
        }

        public static CypherFileItem Create(string inputPath) => new(inputPath, true);
        public static T Create<T>(string inputPath) where T : CypherFileItem => (T)Activator.CreateInstance(typeof(T), inputPath, true);

        public bool TryLoadFooter(bool reload)
        {
            if (Footer != null && !reload)
                return true;

            Footer = FileFooter.Create(InputInfo.FullName);

            if (Footer != null)
            {
                _fileType = Footer.Metadata.FileType;

                EncryptedInfo = new(InputInfo.FullName);
                OriginalInfo = new(Footer.Metadata.OriginalName);
            }
            else
            {
                _fileType = ZFile.GetType(InputInfo.FullName);

                EncryptedInfo = null;
                OriginalInfo = new(InputInfo.FullName);
            }

            return Footer != null;
        }

        public void NotifyAll()
        {
            Notify(null);

            Notify(nameof(IsEnabled));
            Notify(nameof(Kind));
            Notify(nameof(IsCorrupted));
        }

#if USE_THUMBNAIL
        public string Thumbnail { get; private set; }

        public static string GenerateThumbnail(FileInfoBase info, uint maxWidth = 256, uint maxHeight = 256)
        {
            if (info is ImageInfoBase imageInfo)
            {
                var thumbnail = new ImageThumbnail();
                thumbnail.ProcessSync(imageInfo, MagickFormat.Unknown, maxWidth, maxHeight);
                return thumbnail.FilePath;
            }
            else if (info is MediaInfoBase mediaInfo)
            {
                var thumbnail = new MediaThumbnail();
                thumbnail.ProcessSync(mediaInfo, maxWidth, maxHeight);
                return thumbnail.FilePath;
            }

            return null;
        }

        public void LoadThumbnailFromFooter(string password)
        {
            if (Footer?.FooterThumbnailBuffer == null || _fileType.Not(ZFile.FileType.Image, ZFile.FileType.Video)) return;

            var thumbnailBytes = Footer.Extra.IsThumbnailEncrypted ?
                CryptographyUtils.DecryptToBytes(Footer.FooterThumbnailBuffer, password, Constants.SALT, Constants.ITERATIONS, true) :
                Footer.FooterThumbnailBuffer;

            if (thumbnailBytes != null)
                try
                {
                    var thumbnailPath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, $"{Guid.NewGuid()}.bmp");
                    File.WriteAllBytes(thumbnailPath, thumbnailBytes);
                    Thumbnail = thumbnailPath;
                }
                catch { }
        }
#endif

        public static void EncodeOne(string inputFilePath, string outputFilePath, string password, bool overwrite, bool ignoreException = false)
        {
            var outputZipFilePath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, Guid.NewGuid().ToString());
            var outputTempFilePath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, Guid.NewGuid().ToString());

            try
            {
                if (!File.Exists(inputFilePath)) throw new IOException();
                PasswordException.ThrowIfNullOrWhiteSpace(password);

                FileUtils.CreateDirectoryIfNotExist(Path.GetDirectoryName(outputFilePath));

                if (FileUtils.IsFile(inputFilePath))
                    CryptographyUtils.EncryptFile(inputFilePath, outputTempFilePath, password, Constants.SALT, Constants.ITERATIONS);
                else
                {
                    ZipFile.CreateFromDirectory(inputFilePath, outputZipFilePath, CompressionLevel.NoCompression, false);
                    CryptographyUtils.EncryptFile(outputZipFilePath, outputTempFilePath, password, Constants.SALT, Constants.ITERATIONS);
                }

                var inputInfo = new FileInfoBase(inputFilePath);
                var mediaType = ZFile.GetType(inputFilePath);

                var footerExtras = new FooterExtra
                {
                    Size = new FileInfo(outputTempFilePath).Length,
                    CreationTime = TimeExt.DateTimeToUnixTimestamp(inputInfo.CreationTime),
                    LastWriteTime = TimeExt.DateTimeToUnixTimestamp(inputInfo.LastWriteTime),
                    IsThumbnailEncrypted = false,
                };

#if USE_THUMBNAIL
                string thumbnail = null;
                if (mediaType == ZFile.FileType.Image)
                {
                    var info = ImageInfoBase.Create(inputInfo.FullName);
                    info.Analyze();

                    if (!info.IsCorrupted)
                    {
                        footerExtras.Dimension = new(info.MagickImage.Width, info.MagickImage.Height);
                        thumbnail = GenerateThumbnail(info);
                    }
                }
                else if (mediaType.Anys(ZFile.FileType.Video, ZFile.FileType.Audio))
                {
                    var info = MediaInfoBase.Create(inputInfo.FullName);
                    info.Analyze();

                    if (!info.IsCorrupted && info.HasVideo)
                    {
                        footerExtras.Dimension = new(info.Media.Width, info.Media.Height);
                        thumbnail = GenerateThumbnail(info);
                    }
                }

                var encryptedThumbnailBytes = CryptographyUtils.EncryptFileToBytes(thumbnail, password, Constants.SALT, Constants.ITERATIONS);
                footerExtras.IsThumbnailEncrypted = encryptedThumbnailBytes != null;

                FileFooter.AppendToFile(outputTempFilePath, new(mediaType, inputInfo.FullName, footerExtras), encryptedThumbnailBytes);
#else
                FileFooter.AppendToFile(outputTempFilePath, new(mediaType, inputInfo.FullName, footerExtras), null);
#endif

                if (!overwrite)
                    outputFilePath = PathUtils.NextAvailablePath(outputFilePath);

                File.Move(outputTempFilePath, outputFilePath, overwrite);
            }
            catch (Exception)
            {
                if (!ignoreException)
                    throw;
            }
            finally
            {
                FileUtils.Delete(outputTempFilePath);
                FileUtils.Delete(outputZipFilePath);
            }
        }

        public static void DecodeOne(string inputFilePath, string outputFilePath, string password, bool overwrite, bool ignoreException = false)
        {
            var outputRawTempFilePath = Path.Combine(Path.GetDirectoryName(outputFilePath), $"{Guid.NewGuid()}");
            var outputTempFilePath = Path.Combine(Path.GetDirectoryName(outputFilePath), $"{Guid.NewGuid()}");

            try
            {
                if (!File.Exists(inputFilePath)) throw new IOException();
                PasswordException.ThrowIfNullOrWhiteSpace(password);

                FileUtils.CreateDirectoryIfNotExist(Path.GetDirectoryName(outputFilePath));

                File.Copy(inputFilePath, outputRawTempFilePath, true);

                var footer = FileFooter.Create(outputRawTempFilePath) ?? throw new Exception();
                FileUtils.RemoveLastBytesFromFile(outputRawTempFilePath, footer.Size);
                CryptographyUtils.DecryptFile(outputRawTempFilePath, outputTempFilePath, password, Constants.SALT, Constants.ITERATIONS);

                if (!overwrite)
                    outputFilePath = PathUtils.NextAvailablePath(outputFilePath);

                if (footer.Metadata.FileType != ZFile.FileType.Directory)
                    File.Move(outputTempFilePath, outputFilePath, overwrite);
                else
                {
                    FileUtils.CreateDirectoryIfNotExist(outputFilePath);
                    ZipFile.ExtractToDirectory(outputTempFilePath, outputFilePath, overwrite);
                }
            }
            catch (Exception e)
            {
                if (!ignoreException)
                {
                    if (e is ApplicationException)
                        throw new PasswordException(PasswordException.ExceptionKind.NotCorrect);
                    else
                        throw;
                }
            }
            finally
            {
                FileUtils.Delete(outputRawTempFilePath);
                FileUtils.Delete(outputTempFilePath);
            }
        }

        public List<BundleItem<string, string>> GetInfo(bool _) => [
            new(R.T(L.Name),            OriginalInfo.Name),
            new(R.T(L.Location),        OriginalInfo.FolderPath),
            new(R.T(L.FileType),        OriginalInfo.Extension),
            new(R.T(L.FileSize),        FileUtils.GetReadableByteSizeText(Footer?.Extra?.Size != null ? Footer.Extra.Size : InputInfo.FileSize)),
            new(R.T(L.CreationTime),    EncryptedInfo != null ? EncryptedInfo.CreationTime.ToString("f") : InputInfo.CreationTime.ToString("f")),
            new(R.T(L.LastWriteTime),   EncryptedInfo != null ? EncryptedInfo.LastWriteTime.ToString("f") : InputInfo.LastWriteTime.ToString("f"))
        ];
    }
}