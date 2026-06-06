using AuthScape.UserManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Models.Users;

namespace AuthScape.UserManageSystem.Models
{
    public class UserMangementContextSetup
    {
        public static void OnModelCreating(ModelBuilder builder, string newGuidSql)
        {
            builder.Entity<CustomField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.CustomFieldTab)
                    .WithMany(m => m.CustomFieldTabs)
                    .HasForeignKey(rf => rf.TabId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<CustomFieldTab>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });


            builder.Entity<UserCustomField>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.CustomFieldId });

                entity.HasOne(e => e.CustomField)
                    .WithMany(m => m.UserCustomFields)
                    .HasForeignKey(rf => rf.CustomFieldId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<CompanyCustomField>(entity =>
            {
                entity.HasKey(e => new { e.CompanyId, e.CustomFieldId });

                entity.HasOne(e => e.CustomField)
                    .WithMany(m => m.CompanyCustomFields)
                    .HasForeignKey(rf => rf.CustomFieldId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<LocationCustomField>(entity =>
            {
                entity.HasKey(e => new { e.LocationId, e.CustomFieldId });

                entity.HasOne(e => e.CustomField)
                    .WithMany(m => m.LocationCustomFields)
                    .HasForeignKey(rf => rf.CustomFieldId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<Permission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<CompanyDomain>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });
        }
    }
}
