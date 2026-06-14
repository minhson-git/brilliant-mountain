using IOApp.Configs;
using IOCore;
using IOCore.Files;
using IOCore.Utils;
using IOCore.Utils.DataUtils;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace IOApp.Features
{
    public class FooterExtra
    {
        public Size? Dimension { get; set; }
        public long Size { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool IsThumbnailEncrypted { get; set; }

        public static bool IsEmpty(FooterExtra extras) => extras.Dimension is null && extras.Size == 0;

        public string Serialize() => JsonUtils.Serialize(this, FooterExtraJsonContext.Default.FooterExtra);

        public static FooterExtra? Deserialize(string jsonStr)
        {
            try
            {
                return JsonUtils.Deserialize(jsonStr, FooterExtraJsonContext.Default.FooterExtra);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                return null;
            }
        }
    }

    [JsonSerializable(typeof(FooterExtra))]
    partial class FooterExtraJsonContext : JsonSerializerContext { }

    public class FooterMetadata
    {
        public int Version { get; set; } = 1;
        public string Platform { get; set; } = "Windows";
        public bool IsFile { get; set; }
        public ZFile.FileType FileType { get; set; }
        public string OriginalName { get; set; } = "";
        public int ThumbnailSize { get; set; } = 0;
        public string Extras { get; set; } = "";

        public static FooterMetadata? FromMetadataEncryptedBytes(byte[] bytes)
        {
            try
            {
                var decryptedBytes = CryptographyUtils.DecryptToBytes(bytes, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS);
                if (decryptedBytes is null)
                    return null;

                var decryptedStr = Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(decryptedBytes)));
                return JsonUtils.Deserialize(decryptedStr, FooterMetadataJsonContext.Default.FooterMetadata);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                return null;
            }
        }
    }

    [JsonSerializable(typeof(FooterMetadata))]
    partial class FooterMetadataJsonContext : JsonSerializerContext { }

    public class FileFooter
    {
        FileFooter() { }

        public static readonly string SIGNATURE = "63f97fd0fe4081f2d8fea920";

        public byte[] Signature = Encoding.UTF8.GetBytes(SIGNATURE);
        public int MetaSize;
        public FooterMetadata? Metadata { get; private set; }
        public FooterExtra? Extra { get; private set; }

        [JsonIgnore]
        public byte[] MetaSizeBuffer { get; private set; } = [];

        [JsonIgnore]
        public byte[] FooterMetadataBuffer { get; private set; } = [];

        [JsonIgnore]
        public byte[] FooterThumbnailBuffer { get; set; } = [];

        public static FileFooter? Create(string path)
        {
            var footer = new FileFooter();
            return footer.Load(path) ? footer : null;
        }

        public static FileFooter? Create(byte[] bytes)
        {
            var footer = new FileFooter();
            return footer.Load(bytes) ? footer : null;
        }

        public static bool IsLockedFile(string path) => Create(path) is not null;
        public static bool IsLockedFile(byte[] bytes) => Create(bytes) is not null;

        public static void AppendToFile(string path, FooterMetadata metadata, byte[]? thumbnailBytes)
        {
            if (thumbnailBytes is not null) metadata.ThumbnailSize = thumbnailBytes.Length;
            else metadata.ThumbnailSize = 0;

            var metadataB64Str = Convert.ToBase64String(CryptographyUtils.GetJsonByteArrayFromObject(metadata, FooterMetadataJsonContext.Default.FooterMetadata));
            var metadataEncryptedBuffer = CryptographyUtils.EncryptToBytes(metadataB64Str, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS);

            using var fs = new FileStream(path, FileMode.Append);
            using var bw = new BinaryWriter(fs);

            if (thumbnailBytes is not null)
                bw.Write(thumbnailBytes);

            bw.Write(metadataEncryptedBuffer);
            bw.Write(BitConverter.GetBytes(metadataEncryptedBuffer.Length));
            bw.Write(Encoding.UTF8.GetBytes(SIGNATURE));
        }

        bool LoadSignature(FileStream fs)
        {
            fs.Seek(Math.Max(0L, fs.Length - Signature.Length), SeekOrigin.Begin);
            var byteRead = fs.Read(Signature, 0, Signature.Length);

            if (byteRead != Signature.Length)
                return false;

            return SIGNATURE == Encoding.UTF8.GetString(Signature);
        }

        bool LoadSignature(byte[] bytes)
        {
            Signature = new byte[Signature.Length];
            Array.Copy(bytes, bytes.Length - Signature.Length, Signature, 0, Signature.Length);

            return SIGNATURE == Encoding.UTF8.GetString(Signature);
        }

        bool LoadMetaSize(FileStream fs)
        {
            MetaSizeBuffer = new byte[Marshal.SizeOf(MetaSize)];

            fs.Seek(Math.Max(0L, fs.Length - Signature.Length - MetaSizeBuffer.Length), SeekOrigin.Begin);
            var byteRead = fs.Read(MetaSizeBuffer, 0, MetaSizeBuffer.Length);

            if (byteRead != MetaSizeBuffer.Length)
                return false;

            MetaSize = BitConverter.ToInt32(MetaSizeBuffer);
            return MetaSize > 0;
        }

        bool LoadMetaSize(byte[] bytes)
        {
            MetaSizeBuffer = new byte[Marshal.SizeOf(MetaSize)];

            Array.Copy(bytes, bytes.Length - Signature.Length - MetaSizeBuffer.Length, MetaSizeBuffer, 0, MetaSizeBuffer.Length);
            MetaSize = BitConverter.ToInt32(MetaSizeBuffer);
            return MetaSize > 0;
        }

        bool LoadMetadata(FileStream fs)
        {
            FooterMetadataBuffer = new byte[MetaSize];

            fs.Seek(Math.Max(0L, fs.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length), SeekOrigin.Begin);
            var byteRead = fs.Read(FooterMetadataBuffer, 0, FooterMetadataBuffer.Length);

            if (byteRead != FooterMetadataBuffer.Length)
                return false;

            Metadata = FooterMetadata.FromMetadataEncryptedBytes(FooterMetadataBuffer);
            if (Metadata is null)
                return false;

            Extra = FooterExtra.Deserialize(Metadata.Extras);

            return Metadata is not null;
        }

        bool LoadMetadata(byte[] bytes)
        {
            FooterMetadataBuffer = new byte[MetaSize];

            Array.Copy(bytes, bytes.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length, FooterMetadataBuffer, 0, FooterMetadataBuffer.Length);

            Metadata = FooterMetadata.FromMetadataEncryptedBytes(FooterMetadataBuffer);
            if (Metadata is null)
                return false;

            Extra = FooterExtra.Deserialize(Metadata.Extras);

            return Metadata is not null;
        }

        bool LoadThumbnail(FileStream fs)
        {
            if (Metadata is null)
                return false;

            if (Metadata.ThumbnailSize == 0) return true;

            FooterThumbnailBuffer = new byte[Metadata.ThumbnailSize];
            fs.Seek(Math.Max(0L, fs.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length - FooterThumbnailBuffer.Length), SeekOrigin.Begin);
            var byteRead = fs.Read(FooterThumbnailBuffer, 0, FooterThumbnailBuffer.Length);
            if (byteRead != FooterThumbnailBuffer.Length) return false;

            return FooterThumbnailBuffer is not null;
        }

        bool LoadThumbnail(byte[] bytes)
        {
            if (Metadata is null)
                return false;

            if (Metadata.ThumbnailSize == 0) return true;

            FooterThumbnailBuffer = new byte[Metadata.ThumbnailSize];
            Array.Copy(bytes, bytes.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length - FooterThumbnailBuffer.Length, FooterThumbnailBuffer, 0, FooterThumbnailBuffer.Length);

            return FooterThumbnailBuffer is not null;
        }

        public bool Load(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                using var fs = new FileStream(path, FileMode.Open);
                return LoadSignature(fs) && LoadMetaSize(fs) && LoadMetadata(fs) && LoadThumbnail(fs);
            }
            catch
            {
                return false;
            }
        }

        public bool Load(byte[] bytes)
        {
            try { return bytes.Length > 0 && LoadSignature(bytes) && LoadMetaSize(bytes) && LoadMetadata(bytes) && LoadThumbnail(bytes); }
            catch { return false; }
        }

        public long Size => Signature.Length + MetaSizeBuffer.Length + MetaSize + Metadata?.ThumbnailSize ?? 0;
    }
}
