using AuthScape.AccountLinking.Models;
using AuthScape.Analytics.Models;
using AuthScape.Ldap.Models;
using AuthScape.Models.Authentication;
using AuthScape.Models.ErrorTracking;
using AuthScape.Models.Invite;
using AuthScape.Models.Logging;
using AuthScape.Models.Notifications;
using AuthScape.Models.Settings;
using AuthScape.Models.Users;
using AuthScape.Saml2.Models;
using AuthScape.Scim.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Models.Authentication;
using AuthScape.Services.Database;
using System.Linq;

namespace Services.Context
{
    /// <summary>
    /// Auth-only AuthScape DbContext. Only the tables required for authentication, federation,
    /// invites, analytics, logging, error tracking, and in-app notifications are mapped here.
    /// Module-specific tables (Tickets, Documents, CRM, Marketplace, ContentManagement, PrivateLabel,
    /// CustomFields) are intentionally not part of the auth core — bring them back when their modules
    /// are wired in.
    /// </summary>
    public class DatabaseContext : AuthScapeDbContext<AppUser, Role>
    {
        public DatabaseContext(string connectionString) : base(connectionString)
        {
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        // === Multi-tenant org model ===
        public DbSet<Company> Companies { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Settings> Settings { get; set; }

        // === Auth ===
        public DbSet<Fido2Credential> Fido2Credentials { get; set; }
        public DbSet<ThirdPartyAuthentication> ThirdPartyAuthentications { get; set; }

        // === Invite flow ===
        public DbSet<InviteSettings> InviteSettings { get; set; }
        public DbSet<UserInvite> UserInvites { get; set; }

        // === OpenIddict token issuer tables ===
        // NOTE: intentionally NOT declared as DbSets. The OpenIddict entities are registered in the
        // model only when the host opts in via AddAuthScapeDatabase(useOpenIddict: true), which calls
        // options.UseOpenIddict(). On the Keycloak path the flag is false, the entities are absent from
        // the model, and the tables are never created. Access them via Set<T>() in OpenIddict-only code.

        // === Federation ===
        public DbSet<AccountLinkAuditLog> AccountLinkAuditLogs { get; set; }
        public DbSet<LdapConfiguration> LdapConfigurations { get; set; }
        public DbSet<SamlConfiguration> SamlConfigurations { get; set; }
        public DbSet<ScimConfiguration> ScimConfigurations { get; set; }

        // === Always-on platform services ===
        public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; }
        public DbSet<AnalyticsPageView> AnalyticsPageViews { get; set; }
        public DbSet<AnalyticsSession> AnalyticsSessions { get; set; }
        public DbSet<AnalyticsConversion> AnalyticsConversions { get; set; }
        public DbSet<Logging> Loggings { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
        public DbSet<ErrorGroup> ErrorGroups { get; set; }
        public DbSet<ErrorTrackingSettings> ErrorTrackingSettings { get; set; }

        // === Notifications (shipped with the auth core) ===
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }
        public DbSet<NotificationCategoryConfig> NotificationCategoryConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // The OpenIddict-coupled federation config tables (LDAP / SAML / SCIM) are mapped only when
            // the OpenIddict EF integration is active (options.UseOpenIddict(), set by AddAuthScapeDatabase
            // with useOpenIddict: true). On the Keycloak path these features aren't registered, so their
            // tables are excluded from the model and never created — mirroring how the OpenIddict token
            // tables themselves are gated.
            var openIddictActive = this.GetService<IDbContextOptions>().Extensions
                .Any(e => e.GetType().Namespace?.StartsWith("OpenIddict") == true);

            if (!openIddictActive)
            {
                builder.Ignore<LdapConfiguration>();
                builder.Ignore<SamlConfiguration>();
                builder.Ignore<ScimConfiguration>();
            }

            builder.Entity<Settings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);
            });

            #region Invite Settings

            builder.Entity<InviteSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);

                entity.HasIndex(e => e.CompanyId)
                    .IsUnique()
                    .HasFilter(GetNotNullFilter("CompanyId"));

                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserInvite>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);

                entity.HasIndex(e => e.InvitedUserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InviteToken);

                entity.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(e => e.InvitedUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(e => e.InviterId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<Location>()
                    .WithMany()
                    .HasForeignKey(e => e.LocationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            #endregion

            builder.Entity<ThirdPartyAuthentication>(entity =>
            {
                entity.HasKey(e => e.ThirdPartyAuthenticationType);
            });

            builder.Entity<Fido2Credential>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne<AppUser>()
                      .WithMany(u => u.Credentials)
                      .HasForeignKey(e => e.UserId)
                      .HasPrincipalKey(e => e.Id);
            });

            #region Analytics

            builder.Entity<AnalyticsSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);
            });

            builder.Entity<AnalyticsEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);
                entity.HasOne(e => e.Session)
                   .WithMany(s => s.Events)
                   .HasForeignKey(s => s.SessionId)
                   .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<AnalyticsConversion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);
                entity.HasOne(e => e.Session)
                  .WithMany(s => s.Conversions)
                  .HasForeignKey(s => s.SessionId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<AnalyticsPageView>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);
                entity.HasOne(e => e.Session)
                  .WithMany(s => s.PageViews)
                  .HasForeignKey(s => s.SessionId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            #endregion

            #region Org model

            builder.Entity<AppUser>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Company)
                    .WithMany(m => m.Users)
                    .HasForeignKey(rf => rf.CompanyId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.Location)
                    .WithMany(m => m.Users)
                    .HasForeignKey(rf => rf.LocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasIndex(e => new { e.ExternalProvider, e.ExternalSub });
            });

            builder.Entity<Company>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<Location>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Company)
                  .WithMany(u => u.Locations)
                  .HasForeignKey(rf => rf.CompanyId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            #endregion

            #region Notifications

            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);

                entity.HasOne(e => e.Category)
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => e.LocationId);
                entity.HasIndex(e => e.CategoryId);
                entity.HasIndex(e => e.Created);
                entity.HasIndex(e => new { e.UserId, e.IsRead });
            });

            builder.Entity<NotificationPreference>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);

                entity.HasOne(e => e.Category)
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.CategoryId }).IsUnique();
            });

            builder.Entity<NotificationCategoryConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            #endregion

            #region Error Tracking

            builder.Entity<ErrorLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);

                entity.HasIndex(e => e.ErrorGroupId);
                entity.HasIndex(e => e.StatusCode);
                entity.HasIndex(e => e.Source);
                entity.HasIndex(e => e.Environment);
                entity.HasIndex(e => e.Created);
                entity.HasIndex(e => e.IsResolved);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.AnalyticsSessionId);
                entity.HasIndex(e => new { e.Source, e.StatusCode, e.Created });
                entity.HasIndex(e => new { e.IsResolved, e.Created });
            });

            builder.Entity<ErrorGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(NewGuidSql);

                entity.HasIndex(e => e.ErrorSignature).IsUnique();

                entity.HasIndex(e => e.Source);
                entity.HasIndex(e => e.Environment);
                entity.HasIndex(e => e.StatusCode);
                entity.HasIndex(e => e.IsResolved);
                entity.HasIndex(e => e.FirstSeen);
                entity.HasIndex(e => e.LastSeen);
                entity.HasIndex(e => new { e.IsResolved, e.LastSeen });
            });

            builder.Entity<ErrorTrackingSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            #endregion

            // keep at the bottom
            base.OnModelCreating(builder);
        }
    }
}
