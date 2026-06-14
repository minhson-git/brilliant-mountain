using IOCore.Base;
using IOCore.Core;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

#if USE_THUMBNAIL
using IOApp.Configs;
using IOCore.Libs;
using IOCore.Cryptography;
using static IOImage.MagickUtils;
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

        public FileFooter? Footer { get; protected set; }

        public FileInfoBase OriginalInfo { get; }
        public FileInfoBase EncryptedInfo { get; }

        public bool IsCorrupted => Footer is null;
        public CypherKind Kind => Footer?.Metadata is not null ? Footer.Metadata.IsFile ? CypherKind.EncodedFile : CypherKind.EncodedFolder : CypherKind.Normal;

        public CypherFileItem(string path, bool loadFooter = false) : base(path)
        {
            OriginalInfo = new();
            EncryptedInfo = new();

            if (loadFooter)
                TryLoadFooter(false);
        }

        public bool TryLoadFooter(bool reload)
        {
            if (Footer is not null && !reload)
                return true;

            if (InputInfo?.FullName is null)
                return false;

            Footer = FileFooter.Load(InputInfo.FullName);

            if (Footer?.Metadata is not null)
            {
                FileType = Footer.Metadata.FileType;

                OriginalInfo.Refresh(Footer.Metadata.OriginalName);
                EncryptedInfo.Refresh(InputInfo.FullName);
            }
            else
            {
                FileType = ZFile.GetType(InputInfo.FullName);

                OriginalInfo.Refresh(InputInfo.FullName);
                EncryptedInfo.Refresh(null);
            }

            return Footer is not null;
        }

#if USE_THUMBNAIL
        public string? Thumbnail { get; private set; }

        public static string? GenerateThumbnail(FileInfoBase info, uint maxWidth = 256, uint maxHeight = 256)
        {
            // TODO:
            if (info is ImageInfoBase)
            {
                var path = AppDir.LGetHashFilePath(AppDir.Type.TemporaryFolder, info.FullName, "thumb", "bmp");

                using var image = LoadStaticImage(info.FullName, MagickFormat.Unknown, RawImageLoadOption.TryThumbnailOnly, null, maxWidth, maxHeight);
                ArgumentNullException.ThrowIfNull(image);

                image.Write(path, MagickFormat.Bmp);

                return path;
            }
            else if (info is MediaInfoBase mediaInfo && mediaInfo.Media is not null)
            {
                var path = AppDir.LGetHashFilePath(AppDir.Type.TemporaryFolder, info.FullName, "thumb", "bmp");

                var thumbnailProxy = new MediaThumbnailUriProxy();
                thumbnailProxy.Refresh(mediaInfo.FullName);
                thumbnailProxy.SnapshotThumbnail(mediaInfo.Media, path, maxWidth, maxHeight);

                return path;
            }

            return null;
        }

        public MagickImageUriProxy ImageThumbnail { get; } = new();
        public void ThumbnailEnqueued(LzQueue lazyQueue, uint w = 256, uint h = 256, bool needBitmap = false, uint? frameIndex = null)
        {
            if (!ImageThumbnail.IsLoaded)
            {
                ImageThumbnail.Refresh(InputInfo.FullName);
                ImageThumbnail.NeedBitmap = needBitmap;
                ImageThumbnail.FrameIndex = frameIndex;
                lazyQueue.Enqueue(ImageThumbnail.GetThumbnailLzTaskLoader((int)w, (int)h));
            }
        }

        public MediaThumbnailUriProxy MediaThumbnail { get; } = new();
        public void ThumbnailEnqueued(LzQueue lazyQueue)
        {
            if (!MediaThumbnail.IsLoaded)
            {
                MediaThumbnail.Refresh(InputInfo.FullName);
                lazyQueue.Enqueue(MediaThumbnail.GetThumbnailLzTaskLoader());
            }
        }

        public void LoadThumbnailFromFooter(string password)
        {
            if (Footer?.FooterThumbnailBuffer is null || Footer.Extra is null || FileType is not ZFile.FileType.Image and not ZFile.FileType.Video) return;

            var thumbnailBytes = Footer.Extra.IsThumbnailEncrypted
                ? CryptoUtils.DecryptToBytes(Footer.FooterThumbnailBuffer, password, ignoreException: true)
                : Footer.FooterThumbnailBuffer;

            if (thumbnailBytes is not null)
                try
                {
                    var thumbnailPath = AppDir.LGetFilePath(AppDir.Type.TemporaryFolder, $"{Guid.NewGuid()}.bmp");
                    File.WriteAllBytes(thumbnailPath, thumbnailBytes);
                    Thumbnail = thumbnailPath;
                }
                catch { }
        }
#endif

        public static async Task EncodeOne(string inputFilePath, string outputFilePath, string password, bool overwrite, bool ignoreException = false)
        {
            var outputZipFilePath = AppDir.LGetFilePath(AppDir.Type.TemporaryFolder, Guid.NewGuid().ToString());
            var outputTempFilePath = AppDir.LGetFilePath(AppDir.Type.TemporaryFolder, Guid.NewGuid().ToString());

            try
            {
                if (!File.Exists(inputFilePath) && !Directory.Exists(inputFilePath))
                    throw new IOException();

                PasswordException.ThrowIfNullOrWhiteSpace(password);

                FileUtils.CreateDirectoryIfNotExist(Path.GetDirectoryName(outputFilePath));

                if (FileUtils.IsFile(inputFilePath))
                    await FileEncryption.EncryptFileAsync(inputFilePath, outputTempFilePath, password);
                else
                {
                    ZipFile.CreateFromDirectory(inputFilePath, outputZipFilePath, CompressionLevel.NoCompression, false);
                    await FileEncryption.EncryptFileAsync(outputZipFilePath, outputTempFilePath, password);
                }

                var inputInfo = new FileInfoBase(inputFilePath) ?? throw new IOException();
                var mediaType = ZFile.GetType(inputFilePath);

                var footerExtras = new FooterExtra
                {
                    Size = new FileInfo(outputTempFilePath).Length,
                    CreationTime = inputInfo.CreationTime,
                    LastWriteTime = inputInfo.LastWriteTime,
                    IsThumbnailEncrypted = false,
                };

#if USE_THUMBNAIL
                string? thumbnail = null;
                if (mediaType is ZFile.FileType.Image)
                {
                    var info = new ImageInfoBase(inputInfo.FullName);
                    info.Analyze();

                    if (!info.IsCorrupted && info.MagickImageMeta is not null)
                    {
                        footerExtras.Dimension = new((int)info.MagickImageMeta.Width, (int)info.MagickImageMeta.Height);
                        thumbnail = GenerateThumbnail(info) ?? string.Empty;
                    }
                }
                else if (mediaType is ZFile.FileType.Video or ZFile.FileType.Audio)
                {
                    var info = MediaInfoBase.Create(inputInfo.FullName);
                    info.Analyze();

                    if (!info.IsCorrupted && info.HasVideo && info.Media is not null)
                    {
                        footerExtras.Dimension = new((int)info.Media.Width, (int)info.Media.Height);
                        thumbnail = GenerateThumbnail(info);
                    }
                }

                var encryptedThumbnailBytes = FileCryptoUtils.Encrypt(thumbnail, password);
                footerExtras.IsThumbnailEncrypted = encryptedThumbnailBytes is not null;

                var footerMetadata = new FooterMetadata
                {
                    OriginalName = inputInfo.FullName,
                    FileType = mediaType,
                    Extras = FooterExtraJsonContext.Serialize(footerExtras)
                };

                FileFooter.AppendToFile(outputTempFilePath, footerMetadata, encryptedThumbnailBytes);
#else
                var footerMetadata = new FooterMetadata
                {
                    OriginalName = inputInfo.FullName,
                    FileType = mediaType,
                    Extras = FooterExtraJsonContext.Serialize(footerExtras),
                };
                FileFooter.AppendToFile(outputTempFilePath, footerMetadata, null);
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

        public static async Task DecodeOne(string inputFilePath, string outputFilePath, string password, bool overwrite, bool ignoreException = false)
        {
            var outputDirPath = Path.GetDirectoryName(outputFilePath) ?? throw new IOException();

            var outputRawTempFilePath = Path.Combine(outputDirPath, Guid.NewGuid().ToString());
            var outputTempFilePath = Path.Combine(outputDirPath, Guid.NewGuid().ToString());

            try
            {
                if (!File.Exists(inputFilePath)) throw new IOException();

                PasswordException.ThrowIfNullOrWhiteSpace(password);

                FileUtils.CreateDirectoryIfNotExist(outputDirPath);

                File.Copy(inputFilePath, outputRawTempFilePath, true);

                var footer = FileFooter.Load(outputRawTempFilePath) ?? throw new Exception();

                FileUtils.RemoveLastBytesFromFile(outputRawTempFilePath, footer.Size);
                await FileEncryption.DecryptFileAsync(outputRawTempFilePath, outputTempFilePath, password);

                if (!overwrite)
                    outputFilePath = PathUtils.NextAvailablePath(outputFilePath);

                if (footer.Metadata?.FileType is not ZFile.FileType.Directory)
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
                        throw new PasswordException(PasswordException.ExceptionKind.Required);
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

        public List<KeyValueItem<string, string>> GetInfo() => [
            new(R.T(L.Name),            OriginalInfo.Name),
            new(R.T(L.Location),        OriginalInfo.FolderPath),
            new(R.T(L.FileType),        OriginalInfo.Extension),
            new(R.T(L.FileSize),        FileUtils.GetReadableByteSizeText(Footer?.Extra?.Size is not null ? Footer.Extra.Size : InputInfo.FileSize)),
            new(R.T(L.CreationTime),    EncryptedInfo is not null ? EncryptedInfo.CreationTime.ToString("f") : InputInfo.CreationTime.ToString("f")),
            new(R.T(L.LastWriteTime),   EncryptedInfo is not null ? EncryptedInfo.LastWriteTime.ToString("f") : InputInfo.LastWriteTime.ToString("f"))
        ];
    }
}