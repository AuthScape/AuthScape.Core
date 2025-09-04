using Microsoft.EntityFrameworkCore;

namespace AuthScape.ContentManagement.Models
{
    public class ContentManagementSetup
    {
        public static void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Page>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.PageType)
                    .WithMany(m => m.Pages)
                    .HasForeignKey(rf => rf.PageTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.PageRoot)
                    .WithMany(m => m.Pages)
                    .HasForeignKey(rf => rf.PageRootId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<PageType>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<PageRoot>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<PageImageAsset>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<PageBlockList>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }

    }
}
