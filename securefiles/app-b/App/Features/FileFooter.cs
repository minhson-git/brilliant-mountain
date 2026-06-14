using IOApp.Configs;
using IOCore;
using IOCore.Files;
using IOCore.Utils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IOApp.Features
{
    public class LegacyFooterExtra()
    {
        public IOSize<uint>? Dimension { get; set; } = null;

        public long Size { get; set; } = 0;
        public DateTime CreationTime;
        public DateTime LastWriteTime;
        public bool IsThumbnailEncrypted;
    }

    public class FooterExtra()
    {
        public IOSize<uint>? Dimension { get; set; } = null;

        public long Size { get; set; } = 0;
        public ulong CreationTime;
        public ulong LastWriteTime;
        public bool IsThumbnailEncrypted;

        public static bool IsEmpty(FooterExtra extras) => extras.Dimension == null && extras.Size == 0;
        public string ToJson() => JsonConvert.SerializeObject(this);

        public static FooterExtra FromExtraString(string extraString)
        {
            try
            {
                return JsonConvert.DeserializeObject<FooterExtra>(extraString);
            }
            catch
            {
                try
                {
                    var extra = JsonConvert.DeserializeObject<LegacyFooterExtra>(extraString);

                    return new FooterExtra()
                    {
                        Dimension = extra.Dimension,
                        Size = extra.Size,
                        CreationTime = TimeExt.DateTimeToUnixTimestamp(extra.CreationTime),
                        LastWriteTime = TimeExt.DateTimeToUnixTimestamp(extra.LastWriteTime),
                        IsThumbnailEncrypted = extra.IsThumbnailEncrypted
                    };
                }
                catch
                {
                    return new FooterExtra();
                }
            }
        }
    }

    public class FooterMetadata(ZFile.FileType fileType, string originalName, FooterExtra extra)
    {
        public int Version = 1;
        public string Platform = "Windows";
        public bool IsFile = fileType != ZFile.FileType.Directory;
        public ZFile.FileType FileType = fileType;
        public string OriginalName = originalName;
        public int ThumbnailSize = 0;
        public string Extras = (extra ?? new()).ToJson();

        public static FooterMetadata FromMetadataEncryptedBytes(byte[] bytes)
        {
            try
            {
                return JsonConvert.DeserializeObject<FooterMetadata>(
                    Encoding.UTF8.GetString(
                        Convert.FromBase64String(
                            Encoding.UTF8.GetString(
                                CryptographyUtils.DecryptToBytes(bytes, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS)))));
            }
            catch
            {
                return JsonConvert.DeserializeObject<FooterMetadata>(Encoding.UTF8.GetString(bytes));
            }
        }
    }

    public class FileFooter
    {
        public static readonly string SIGNATURE = "63f97fd0fe4081f2d8fea920";

        public byte[] Signature = Encoding.UTF8.GetBytes(SIGNATURE);
        public int MetaSize;
        public FooterMetadata Metadata { get; private set; }
        public FooterExtra Extra { get; private set; }

        [JsonIgnore]
        public byte[] MetaSizeBuffer { get; private set; }

        [JsonIgnore]
        public byte[] FooterMetadataBuffer { get; private set; }

        [JsonIgnore]
        public byte[] FooterThumbnailBuffer { get; set; }

        FileFooter() { }

        public static FileFooter Create() => new();

        public static FileFooter Create(string path)
        {
            var footer = new FileFooter();
            return footer.Load(path) ? footer : null;
        }

        public static FileFooter Create(byte[] bytes)
        {
            var footer = new FileFooter();
            return footer.Load(bytes) ? footer : null;
        }

        public static bool IsLockedFile(string path) => Create(path) != null;
        public static bool IsLockedFile(byte[] bytes) => Create(bytes) != null;

        public static void AppendToFile(string path, FooterMetadata metadata, byte[] thumbnailBytes)
        {
            if (thumbnailBytes != null) metadata.ThumbnailSize = thumbnailBytes.Length;
            else metadata.ThumbnailSize = 0;

            var metadataB64Str = Convert.ToBase64String(CryptographyUtils.GetJsonByteArrayFromObject(metadata));
            var metadataEncryptedBuffer = CryptographyUtils.EncryptToBytes(metadataB64Str, Constants.TOKEN, Constants.SALT, Constants.ITERATIONS);

            using var fs = new FileStream(path, FileMode.Append);
            using var bw = new BinaryWriter(fs);

            if (thumbnailBytes != null)
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
            Extra = FooterExtra.FromExtraString(Metadata.Extras);

            return Metadata != null;
        }

        bool LoadMetadata(byte[] bytes)
        {
            FooterMetadataBuffer = new byte[MetaSize];

            Array.Copy(bytes, bytes.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length, FooterMetadataBuffer, 0, FooterMetadataBuffer.Length);

            Metadata = FooterMetadata.FromMetadataEncryptedBytes(FooterMetadataBuffer);
            Extra = FooterExtra.FromExtraString(Metadata.Extras);

            return Metadata != null;
        }

        bool LoadThumbnail(FileStream fs)
        {
            if (Metadata.ThumbnailSize == 0) return true;

            FooterThumbnailBuffer = new byte[Metadata.ThumbnailSize];
            fs.Seek(Math.Max(0L, fs.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length - FooterThumbnailBuffer.Length), SeekOrigin.Begin);
            var byteRead = fs.Read(FooterThumbnailBuffer, 0, FooterThumbnailBuffer.Length);
            if (byteRead != FooterThumbnailBuffer.Length) return false;

            return FooterThumbnailBuffer != null;
        }

        bool LoadThumbnail(byte[] bytes)
        {
            if (Metadata.ThumbnailSize == 0) return true;

            FooterThumbnailBuffer = new byte[Metadata.ThumbnailSize];
            Array.Copy(bytes, bytes.Length - Signature.Length - MetaSizeBuffer.Length - FooterMetadataBuffer.Length - FooterThumbnailBuffer.Length, FooterThumbnailBuffer, 0, FooterThumbnailBuffer.Length);
            
            return FooterThumbnailBuffer != null;
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

        public long Size => Signature.Length + MetaSizeBuffer.Length + MetaSize + Metadata.ThumbnailSize;
    }
}
