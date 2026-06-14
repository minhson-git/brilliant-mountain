using IOCore.Files;
using IOData;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace IOApp.Features
{
    [Table("Files")]
    public class SecuredFileEntity(string path) : CoreEntity
    {
        public string Path { get; set; } = path;

        public ZFile.FileType FileType { get; set; }

        public PrivateFileItem.CensorType CensorType { get; set; }

        public bool IsFavorite { get; set; }

        public string Note { get; set; }
    }

    internal partial class AppDbContext(string baseDir) : CoreDbContext(baseDir)
    {
        public DbSet<SecuredFileEntity> Files { get; private set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);

            if (!options.IsConfigured)
                options.UseModel(CompiledModels.AppDbContextModel.Instance);
        }

        public override void Verify()
        {
            try
            {
                _ = Files.FirstOrDefault();
            }
            catch
            {
                throw;
            }
        }
    }
}