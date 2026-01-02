using Microsoft.EntityFrameworkCore;


namespace AuthScape.Marketplace.Models
{
    public class MarketplaceContextSetup
    {
        public static void OnModelCreating(ModelBuilder builder)
        {
            //builder.Entity<ProductCard>(entity =>
            //{
            //    entity.HasKey(e => e.Id);
            //    entity.Property(e => e.Id).HasDefaultValueSql("newsequentialid()");
            //});

            builder.Entity<ProductCardCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("newsequentialid()");

                // Add composite index for common query pattern (CompanyId, PlatformId, Name)
                entity.HasIndex(e => new { e.CompanyId, e.PlatformId, e.Name })
                      .HasDatabaseName("IX_ProductCardCategory_Company_Platform_Name");
            });

            //builder.Entity<ProductCardField>(entity =>
            //{
            //    entity.HasKey(e => e.Id);
            //    entity.Property(e => e.Id).HasDefaultValueSql("newsequentialid()");

            //    //entity.HasOne(e => e.ProductCategory)
            //    //    .WithMany(m => m.ProductFields)
            //    //    .HasForeignKey(rf => rf.ProductCategoryId)
            //    //    .OnDelete(DeleteBehavior.ClientSetNull);
            //});

            //builder.Entity<ProductCardAndCardFieldMapping>(entity =>
            //{
            //    entity.HasKey(e => new { e.Id, e.ProductId, e.ProductFieldId });
            //    entity.Property(e => e.Id).HasDefaultValueSql("newsequentialid()");

            //    //entity.HasOne(e => e.Product)
            //    //    .WithMany(m => m.ProductCardAndCardFieldMapping)
            //    //    .HasForeignKey(rf => rf.ProductId)
            //    //    .OnDelete(DeleteBehavior.ClientSetNull);

            //    //entity.HasOne(e => e.ProductField)
            //    //    .WithMany(m => m.ProductCardAndCardFieldMapping)
            //    //    .HasForeignKey(rf => rf.ProductFieldId)
            //    //    .OnDelete(DeleteBehavior.ClientSetNull);
            //});

            builder.Entity<AnalyticsMarketplaceImpressionsClicks>(entity =>
            {
                entity.HasKey(e => new { e.Id });
                entity.Property(e => e.Id).HasDefaultValueSql("newsequentialid()");
            });

        }
    }
}
