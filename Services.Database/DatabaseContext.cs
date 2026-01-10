using AuthScape.Analytics.Models;
using AuthScape.ContentManagement.Models;
using AuthScape.Document.Mapping.Models;
using AuthScape.Document.Models;
using AuthScape.Marketplace.Models;
using AuthScape.Models.Authentication;
using AuthScape.Models.Invite;
using AuthScape.Models.Logging;
using AuthScape.Models.Marketing;
using AuthScape.Models.PaymentGateway;
using AuthScape.Models.PaymentGateway.Coupons;
using AuthScape.Models.PaymentGateway.Plans;
using AuthScape.Models.PaymentGateway.Stripe;
using AuthScape.Models.Settings;
using AuthScape.Models.Stylesheets;
using AuthScape.Models.Users;
using AuthScape.NodeService.Models;
using AuthScape.Plugins.Invoices.Models;
using AuthScape.PrivateLabel.Models;
using AuthScape.TicketSystem.Modals;
using AuthScape.UserManagementSystem.Models;
using AuthScape.UserManageSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Authentication;
using Models.Kanban;
using Models.Users;
using OpenIddict.EntityFrameworkCore.Models;
using Services.Database;

namespace Services.Context
{
    public class DatabaseContext : IdentityDbContext<AppUser, Role, long>
    {
        /// <summary>
        /// Creates a DatabaseContext with auto-detected provider based on connection string format.
        /// </summary>
        public DatabaseContext(string connectionString) : base(GetOptions(connectionString))
        {
        }

        private static DbContextOptions GetOptions(string connectionString)
        {
            return DatabaseProviderExtensions.GetOptions(connectionString);
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        /// <summary>
        /// Gets the current database provider based on the configured options.
        /// </summary>
        private DatabaseProvider CurrentProvider
        {
            get
            {
                if (Database.IsSqlServer()) return DatabaseProvider.SqlServer;
                if (Database.IsNpgsql()) return DatabaseProvider.PostgreSQL;
                if (Database.IsSqlite()) return DatabaseProvider.SQLite;
                // MySQL detection will be added when Pomelo releases .NET 10 compatible version
                return DatabaseProvider.SqlServer; // Default fallback
            }
        }

        /// <summary>
        /// Gets the appropriate UUID/GUID generation SQL for the current database provider.
        /// </summary>
        private string GetNewGuidSql()
        {
            return CurrentProvider switch
            {
                DatabaseProvider.SqlServer => "newsequentialid()",
                DatabaseProvider.PostgreSQL => "gen_random_uuid()",
                DatabaseProvider.MySQL => "(UUID())",
                DatabaseProvider.SQLite => "(lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))))",
                _ => "newsequentialid()"
            };
        }

        /// <summary>
        /// Gets the appropriate filter syntax for nullable column checks.
        /// SQL Server uses [Column] IS NOT NULL, PostgreSQL and MySQL use "Column" IS NOT NULL or Column IS NOT NULL
        /// </summary>
        private string GetNotNullFilter(string columnName)
        {
            return CurrentProvider switch
            {
                DatabaseProvider.SqlServer => $"[{columnName}] IS NOT NULL",
                DatabaseProvider.PostgreSQL => $"\"{columnName}\" IS NOT NULL",
                DatabaseProvider.MySQL => $"`{columnName}` IS NOT NULL",
                DatabaseProvider.SQLite => $"\"{columnName}\" IS NOT NULL",
                _ => $"[{columnName}] IS NOT NULL"
            };
        }

        public DbSet<UserLocations> UserLocations { get; set; }

        public DbSet<Page> Pages { get; set; }
        public DbSet<PageType> PageTypes { get; set; }
        public DbSet<PageRoot> PageRoots { get; set; }
        public DbSet<PageBlockList> PageBlockLists { get; set; }
        public DbSet<PageImageAsset> PageImageAssets { get; set; }
        public DbSet<Stylesheet> Stylesheets { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Fido2Credential> Fido2Credentials { get; set; }
        public DbSet<Settings> Settings { get; set; }

        public DbSet<CompanyDomain> CompanyDomains { get; set; }

        #region Marketplace

        //public DbSet<ProductCard> ProductCards { get; set; }
        public DbSet<ProductCardCategory> ProductCardCategories { get; set; }
        //public DbSet<ProductCardField> ProductCardFields { get; set; }
        //public DbSet<ProductCardAndCardFieldMapping> ProductCardAndCardFieldMapping { get; set; }
        public DbSet<AnalyticsMarketplaceImpressionsClicks> AnalyticsMarketplaceImpressionsClicks { get; set; }

        #endregion

        #region UserManagement

        public DbSet<CustomField> CustomFields { get; set; }
        public DbSet<UserCustomField> UserCustomFields { get; set; }
        public DbSet<CompanyCustomField> CompanyCustomFields { get; set; }
        public DbSet<LocationCustomField> LocationCustomFields { get; set; }


        public DbSet<CustomFieldTab> CustomFieldsTab { get; set; }
        public DbSet<Permission> Permissions { get; set; }

        #endregion

        #region Invite

        public DbSet<InviteSettings> InviteSettings { get; set; }
        public DbSet<UserInvite> UserInvites { get; set; }

        #endregion

        #region PaymentGateway

        public DbSet<Plan> Plans { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<WalletPaymentMethod> WalletPaymentMethods { get; set; }
        public DbSet<StoreCredit> StoreCredits { get; set; }
        public DbSet<StripeConnectAccount> StripeConnectAccounts { get; set; }

        // Stripe Subscriptions & Invoices
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionItem> SubscriptionItems { get; set; }
        public DbSet<StripeInvoice> StripeInvoices { get; set; }
        public DbSet<StripeInvoiceLineItem> StripeInvoiceLineItems { get; set; }

        // Subscription Plan Management
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<SubscriptionPlanRole> SubscriptionPlanRoles { get; set; }

        // Promo Codes
        public DbSet<PromoCode> PromoCodes { get; set; }

        #endregion

        #region OpenIdDict

        public DbSet<OpenIddictEntityFrameworkCoreApplication> OpenIddictApplications { get; set; }
        public DbSet<OpenIddictEntityFrameworkCoreAuthorization> OpenIddictAuthorizations { get; set; }
        public DbSet<OpenIddictEntityFrameworkCoreScope> OpenIddictScopes { get; set; }
        public DbSet<OpenIddictEntityFrameworkCoreToken> OpenIddictTokens { get; set; }

        #endregion

        #region Coupons

        public DbSet<Coupon> Coupons { get; set; }
        //public DbSet<ProductCoupon> ProductCoupons { get; set; }

        #endregion

        #region Invoice System

        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }
        public DbSet<InvoiceLineItemsName> InvoiceLineItemNames { get; set; }
        public DbSet<InvoicePayment> InvoicePayments { get; set; }

        #endregion

        #region Inventory / Products

        //public DbSet<Product> Products { get; set; }
        //public DbSet<ProductCategory> ProductCategories { get; set; }

        #endregion

        #region Ticket System

        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<TicketStatus> TicketStatuses { get; set; }
        public DbSet<TicketType> TicketTypes { get; set; }
        public DbSet<TicketParticipant> TicketParticipants { get; set; }
        public DbSet<TicketAttachment> TicketAttachments { get; set; }

        #endregion

        #region Documents

        public DbSet<DocumentItem> Documents { get; set; }
        public DbSet<DocumentFolder> DocumentFolders { get; set; }
        public DbSet<DocumentSegment> DocumentSegments { get; set; }
        public DbSet<SharedDocument> SharedDocuments { get; set; }

        #endregion

        public DbSet<SomeSheet> SomeSheet { get; set; }

        //public DbSet<Sheet> Sheets { get; set; }
        public DbSet<DocumentSheet> Sheets { get; set; }
        public DbSet<Attribute> Attributes { get; set; }
        public DbSet<SheetAttribute> SheetAttributes { get; set; }

        #region MappingSystem 

        //public DbSet<DocumentAttributeHeader> DocumentAttributeHeader { get; set; }
        //public DbSet<DocumentAttributeValue> DocumentAttributeValue { get; set; }
        public DbSet<DocumentMapping> DocumentMappings { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<DocumentComponent> DocumentComponents { get; set; }
        public DbSet<DocumentMatchMemory> DocumentMatchMemories { get; set; }

        #endregion

        #region Analytics Module

        public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; }
        public DbSet<AnalyticsPageView> AnalyticsPageViews { get; set; }
        public DbSet<AnalyticsSession> AnalyticsSessions { get; set; }
        public DbSet<AnalyticsConversion> AnalyticsConversions { get; set; }
        public DbSet<AnalyticsMailTracking> AnalyticsMailTrackings { get; set; }
        public DbSet<AnalyticsMail> AnalyticsMails { get; set; }

        #endregion

        #region Logging

        public DbSet<Logging> Loggings { get; set; }

        #endregion

        #region OEM Module

        public DbSet<DnsRecord> DnsRecords { get; set; }
        public DbSet<PrivateLabelField> PrivateLabelFields { get; set; }
        public DbSet<PrivateLabelSelectedFields> PrivateLabelSelectedFields { get; set; }


        #endregion

        #region NodeService Module

        public DbSet<FlowProject> FlowProjects { get; set; }
        public DbSet<FlowNode> FlowNodes { get; set; }
        public DbSet<FlowEdge> FlowEdges { get; set; }
        public DbSet<FlowViewport> FlowViewports { get; set; }

        #endregion

        #region Kanban

        public DbSet<KanbanCard> KanbanCards { get; set; }
        public DbSet<KanbanColumn> KanbanColumns { get; set; }
        public DbSet<KanbanAssignedTo> KanbanAssignedTos { get; set; }
        public DbSet<KanbanCardCollaborator> KanbanCardCollaborators { get; set; }

        #endregion

        #region ThirdPartyAuthentication

        public DbSet<ThirdPartyAuthentication> ThirdPartyAuthentications { get; set; }

        #endregion

        #region Interest Signups (CommandDeck Marketing)

        public DbSet<InterestSignup> InterestSignups { get; set; }

        #endregion

        protected override void OnModelCreating(ModelBuilder builder)
        {
            TicketContextSetup.OnModelCreating(builder);

            DocumentContextSetup.OnModelCreating(builder);

            RegisterUserManagementService.OnModelCreating(builder);

            UserMangementContextSetup.OnModelCreating(builder);

            MarketplaceContextSetup.OnModelCreating(builder);

            ContentManagementSetup.OnModelCreating(builder);


            // Get provider-specific SQL for GUID generation
            var newGuidSql = GetNewGuidSql();

            builder.Entity<Settings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            #region Invite Settings

            builder.Entity<InviteSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                // Unique constraint: only one global (null) and one per company
                entity.HasIndex(e => e.CompanyId)
                    .IsUnique()
                    .HasFilter(GetNotNullFilter("CompanyId"));

                // Configure foreign key without navigation property
                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserInvite>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasIndex(e => e.InvitedUserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InviteToken);

                // Configure foreign keys without navigation properties
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

                // Configure relationship with correct types
                entity.HasOne(e => e.User)
                      .WithMany(e => e.Credentials)
                      .HasForeignKey(e => e.UserId)
                      .HasPrincipalKey(e => e.Id); // Match ApplicationUser's long Id
            });


            

            



            #region Documents

            builder.Entity<SharedDocument>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.DocumentId });
            });

            #endregion

            #region Private Label Module

            builder.Entity<DnsRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<PrivateLabelField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<PrivateLabelSelectedFields>(entity =>
            {
                entity.HasKey(e => new { e.DnsRecordId, e.PrivateLabelFieldId });

                entity.HasOne(e => e.DnsRecord)
                  .WithMany(u => u.PrivateLabelSelectedFields)
                  .HasForeignKey(rf => rf.DnsRecordId)
                  .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.PrivateLabelField)
                  .WithMany(u => u.PrivateLabelSelectedFields)
                  .HasForeignKey(rf => rf.PrivateLabelFieldId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });



            #endregion

            #region Invoices

            builder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<InvoiceLineItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.Invoice)
                  .WithMany(u => u.InvoiceLineItems)
                  .HasForeignKey(rf => rf.InvoiceId)
                  .OnDelete(DeleteBehavior.ClientSetNull);


                entity.HasOne(e => e.InvoiceLineItemName)
                  .WithMany(u => u.InvoiceLineItems)
                  .HasForeignKey(rf => rf.InvoiceLineItemNameId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<InvoiceLineItemsName>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<InvoicePayment>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Invoice)
                  .WithMany(u => u.InvoicePayments)
                  .HasForeignKey(rf => rf.InvoiceId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            #endregion

            #region FlowService

            builder.Entity<FlowNode>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.FlowProject)
                    .WithMany(m => m.Nodes)
                    .HasForeignKey(rf => rf.FlowProjectId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<FlowEdge>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.FlowProject)
                    .WithMany(m => m.Edges)
                    .HasForeignKey(rf => rf.FlowProjectId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<FlowViewport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.FlowProject)
                    .WithMany(m => m.Viewports)
                    .HasForeignKey(rf => rf.FlowProjectId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });


            #endregion

            #region Analytics

            builder.Entity<AnalyticsSession>(entity =>
            {

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

            });

            


            builder.Entity<AnalyticsMailTracking>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
                entity.HasOne(e => e.AnalyticsMail)
                   .WithMany(s => s.AnalyticsMailTracking)
                   .HasForeignKey(s => s.AnalyticsMailId)
                   .OnDelete(DeleteBehavior.ClientSetNull);
            });


            builder.Entity<AnalyticsMail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });


            builder.Entity<AnalyticsEvent>(entity =>
            {

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
                entity.HasOne(e => e.Session)
                   .WithMany(s => s.Events)
                   .HasForeignKey(s => s.SessionId)
                   .OnDelete(DeleteBehavior.ClientSetNull);

            });

            builder.Entity<AnalyticsConversion>(entity =>
            {

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
                entity.HasOne(e => e.Session)
                  .WithMany(s => s.Conversions)
                  .HasForeignKey(s => s.SessionId)
                  .OnDelete(DeleteBehavior.ClientSetNull);

            });

            builder.Entity<AnalyticsPageView>(entity =>
            {

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
                entity.HasOne(e => e.Session)
                  .WithMany(s => s.PageViews)
                  .HasForeignKey(s => s.SessionId)
                  .OnDelete(DeleteBehavior.ClientSetNull);

            });

            #endregion

            #region Product and Mapping



            builder.Entity<DocumentSheet>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<Attribute>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.DocumentComponent)
                    .WithMany(m => m.Attributes)
                    .HasForeignKey(rf => rf.DocumentComponentId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<SheetAttribute>(entity =>
            {
                entity.HasKey(e => new { e.ProductId, e.AttributeId });

                entity.HasOne(e => e.Attribute)
                    .WithMany(m => m.SheetAttributes)
                    .HasForeignKey(rf => rf.AttributeId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.Sheet)
                    .WithMany(m => m.SheetAttributes)
                    .HasForeignKey(rf => rf.ProductId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });


            #endregion

            #region Kanban Setup

            builder.Entity<KanbanColumn>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });


            builder.Entity<KanbanCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.KanbanColumn)
                    .WithMany(m => m.Cards)
                    .HasForeignKey(rf => rf.KanbanColumnId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<KanbanCardCollaborator>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<KanbanAssignedTo>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.KanbanCardId });

                entity.HasOne(e => e.KanbanCard)
                    .WithMany(m => m.KanbanAssignedTos)
                    .HasForeignKey(rf => rf.KanbanCardId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            #endregion


            builder.Entity<Wallet>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<WalletPaymentMethod>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Wallet)
                    .WithMany(m => m.WalletPaymentMethods)
                    .HasForeignKey(rf => rf.WalletId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.Wallet)
                    .WithMany(m => m.Subscriptions)
                    .HasForeignKey(rf => rf.WalletId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasIndex(e => e.StripeSubscriptionId).IsUnique();
            });

            builder.Entity<SubscriptionItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.Subscription)
                    .WithMany(m => m.Items)
                    .HasForeignKey(rf => rf.SubscriptionId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasIndex(e => e.StripeSubscriptionItemId).IsUnique();
            });

            builder.Entity<StripeInvoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.Wallet)
                    .WithMany()
                    .HasForeignKey(rf => rf.WalletId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.Subscription)
                    .WithMany()
                    .HasForeignKey(rf => rf.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.StripeInvoiceId).IsUnique();
            });

            builder.Entity<StripeInvoiceLineItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasOne(e => e.StripeInvoice)
                    .WithMany(m => m.LineItems)
                    .HasForeignKey(rf => rf.StripeInvoiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            // ===== SubscriptionPlan Configuration =====
            builder.Entity<SubscriptionPlan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                entity.HasIndex(e => e.StripePriceId).IsUnique().HasFilter(GetNotNullFilter("StripePriceId"));
                entity.HasIndex(e => e.StripeProductId);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.SortOrder);
            });

            builder.Entity<SubscriptionPlanRole>(entity =>
            {
                entity.HasKey(e => new { e.SubscriptionPlanId, e.RoleId });

                entity.HasOne(e => e.SubscriptionPlan)
                    .WithMany(sp => sp.AllowedRoles)
                    .HasForeignKey(e => e.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== PromoCode Configuration =====
            builder.Entity<PromoCode>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);

                // Unique index on Code
                entity.HasIndex(e => e.Code).IsUnique();

                // Indexes for Stripe IDs
                entity.HasIndex(e => e.StripeCouponId).HasFilter(GetNotNullFilter("StripeCouponId"));
                entity.HasIndex(e => e.StripePromotionCodeId).HasFilter(GetNotNullFilter("StripePromotionCodeId"));

                // Performance indexes
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.Scope);
                entity.HasIndex(e => e.ExpiresAt);
            });


            builder.Entity<SomeSheet>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });


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







            #region Document Mapping


            builder.Entity<DocumentMatchMemory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<DocumentMapping>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.DocumentComponents)
                    .WithMany(m => m.DocumentMappings)
                    .HasForeignKey(rf => rf.DocumentComponentId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });


            builder.Entity<DocumentType>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            builder.Entity<DocumentComponent>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.HeaderRow).HasDefaultValue(1);

                entity.HasOne(e => e.DocumentType)
                    .WithMany(m => m.DocumentComponents)
                    .HasForeignKey(rf => rf.DocumentTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });


            #endregion







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
            });

            builder.Entity<Company>(entity =>
            {
                entity.HasKey(e => e.Id);
            });


            builder.Entity<ProductCardCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql(newGuidSql);
            });

            builder.Entity<Location>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Company)
                  .WithMany(u => u.Locations)
                  .HasForeignKey(rf => rf.CompanyId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<UserLocations>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.LocationId });

                entity.HasOne(e => e.User)
                    .WithMany(m => m.UserLocations)
                    .HasForeignKey(rf => rf.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.Location)
                    .WithMany(m => m.UserLocations)
                    .HasForeignKey(rf => rf.LocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<Wallet>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.User)
                  .WithMany(u => u.Cards)
                  .HasForeignKey(rf => rf.UserId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            builder.Entity<StoreCredit>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.User)
                  .WithMany(u => u.StoreCredits)
                  .HasForeignKey(rf => rf.UserId)
                  .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.GiftFromUser)
                  .WithMany(u => u.GiftedCredit)
                  .HasForeignKey(rf => rf.GiftFromId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
            });

            // keep at the bottom
            base.OnModelCreating(builder);
        }
    }
}
