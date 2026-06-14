using IOCore.Files;
using IOData;
using System;
using System.Linq;

namespace IOApp.Features
{
    public partial class PrivateFileItem(string path) : CypherFileItem(path)
    {
        [Obsolete]
        public enum PrivacyType
        {
            None,
            Blur,
        };

        public enum CensorType
        {
            None,
            Blur,
        };

        protected CensorType _censor;
        public CensorType Censor { get => _censor; set => SetAndNotify(ref _censor, value); }

        public string RecoveredFileOrFolderPath;

        public string ExportedFilePath { get; set; }
        public void NotifyExportedFilePath() => Notify(nameof(ExportedFilePath));

        public static PrivateFileItem Create(SecuredFileEntity entity)
        {
            var item = Create<PrivateFileItem>(entity.Path);

            item.EncryptedInfo = new(entity.Path);

            item._fileType = entity.FileType;
            item._censor = entity.CensorType;

            return item;
        }

        public void CorrectFileType(ZFile.FileType fileType) => _fileType = fileType;

        public void Removed()
        {
            DBManager.Inst.Execute<AppDbContext>(context =>
            {
                var fileEntity = context.Files.FirstOrDefault(i => i.Path == EncryptedInfo.FullName);
                if (fileEntity != null)
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