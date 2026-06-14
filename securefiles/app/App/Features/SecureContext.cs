using IOCore.Base;
using IOCore.ConcurrentCollections;
using IOCore.Files;
using IOData;
using System.Linq;

namespace IOApp.Features
{
    public class SecureContext : Singleton<SecureContext>
    {
        SecureContext() { }

        public readonly ConcurrentList<SecureFileItem> SECURE_ITEMS = [];
    }

    public partial class SecureFileItem : CypherFileItem
    {
        public enum CensorType
        {
            None,
            Blur,
        };

        protected CensorType _censor;
        public CensorType Censor { get => _censor; set => SetAndNotify(ref _censor, value); }

        public string FileSizeStr => FileUtils.GetReadableByteSizeText(EncryptedInfo?.FileSize ?? 0);

        public string? RecoveredFileOrFolderPath;

        public SecureFileItem(string path, bool loadFooter = false) : base(path, loadFooter)
        {
        }

        public SecureFileItem(SecureFileEntity entity, bool loadFooter = false) : base(entity.Path, loadFooter)
        {
            EncryptedInfo.Refresh(entity.Path);

            _fileType = entity.FileType;
            _censor = entity.CensorType;
        }

        public string? ExportedFilePath { get; set; }

        public void CorrectFileType(ZFile.FileType fileType) => _fileType = fileType;

        public void Removed()
        {
            if (EncryptedInfo is null)
                return;

            DBManager.Inst.Execute<AppDbContext>(context =>
            {
                var fileEntity = context.Files.FirstOrDefault(i => i.Path == EncryptedInfo.FullName);
                if (fileEntity is not null)
                {
                    context.Files.Remove(fileEntity);
                    context.SaveChanges();

                    FileUtils.Delete(EncryptedInfo.FullName);
                    FileUtils.Delete(RecoveredFileOrFolderPath);
                }
            });
        }
    }
}