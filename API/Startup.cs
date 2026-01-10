using Authscape.IdentityServer.Services;
using Authscape.Reporting.Services;
using AuthScape.Analytics.Services;
using AuthScape.AzureCloudService;
using AuthScape.ContentManagement.Models.Hubs;
using AuthScape.ContentManagement.Services;
using AuthScape.Controllers;
using AuthScape.Document.Mapping.Services;
using AuthScape.DocumentProcessing.Services;
using AuthScape.Flows.Services;
using AuthScape.Kanban.Services;
using AuthScape.Logging.Services;
using AuthScape.Marketplace.Services;
using AuthScape.Models.Sitemap;
using AuthScape.Models.Users;
using AuthScape.OpenAI;
using AuthScape.PrivateLabel.Services;
using AuthScape.ReadMail;
using AuthScape.SendGrid;
using AuthScape.Services;
using AuthScape.Services.Azure.Storage;
using AuthScape.Services.PromoCode;
using AuthScape.Services.Subscription;
using AuthScape.Spreadsheet;
using AuthScape.Spreadsheet.Models.Hubs;
using AuthScape.StripePayment.Services;
using AuthScape.TicketSystem.Services;
using AuthScape.UserManageSystem.Services;
using Authsome;
using CoreBackpack.Azure;
using CoreBackpack.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services;
using Services.Context;
using Services.Database;
using System;

namespace API
{
    public class Startup
    {
        readonly IWebHostEnvironment _currentEnvironment;
        private AuthenticationManager authenticationManager;
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _currentEnvironment = env;
            authenticationManager = new AuthenticationManager();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            authenticationManager.RegisterConfigureServices(Configuration, _currentEnvironment, services, (builder) =>
            {
                services.AddIdentity<AppUser, Role>()
                    .AddEntityFrameworkStores<DatabaseContext>()
                    .AddDefaultTokenProviders();

                builder.AddValidation(options =>
                 {
                     // Note: the validation handler uses OpenID Connect discovery
                     // to retrieve the issuer signing keys used to validate tokens.
                     options.SetIssuer("https://localhost:44303/");
                     options.AddAudiences("resource_server_1");

                     // Configure the validation handler to use introspection and register the client
                     // credentials used when communicating with the remote introspection endpoint.
                     options.UseIntrospection()
                             .SetClientId("resource_server_1")
                             .SetClientSecret("846B62D0-DEF9-4215-A99D-86E6B8DAB342");

                     // Register the System.Net.Http integration.
                     options.UseSystemNetHttp();

                     // Register the ASP.NET Core host.
                     options.UseAspNetCore();
                 });
            }, (scope) =>
            {
                // provide additional scopes here...
                services.AddScoped<CoreBackpack.Services.ISlugService, CoreBackpack.Services.SlugService>();
                services.AddScoped<IMailService, MailService>();
                services.AddScoped<IStoreCreditService, StoreCreditService>();
                services.AddScoped<ICompaniesService, CompaniesService>();
                services.AddScoped<ISendGridService, SendGridService>();
                services.AddScoped<IUserService, UserService>();
                services.AddScoped<IStripePayService, StripePayService>();
                services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
                services.AddScoped<IStripeInvoiceService, StripeInvoiceService>();
                services.AddScoped<IAchVerificationEmailService, AchVerificationEmailService>();
                services.AddScoped<ITicketService, TicketService>();
                services.AddScoped<IPrivateLabelService, PrivateLabelService>();
                services.AddScoped<IAuthsomeService, AuthsomeService>();
                services.AddScoped<IIdentityServerService, IdentityServerService>();
                services.AddScoped<ILogService, LogService>();
                services.AddScoped<INotificationService, NotificationService>();

                services.AddScoped<IContentManagementService, ContentManagementService>();

                services.AddScoped<IOpenAIService, OpenAIService>();
                services.AddScoped<IDocumentService, DocumentService>();

                services.AddScoped<IBlobStorage, BlobStorage>();
                services.AddScoped<IImageService, ImageService>();
                services.AddScoped<IAzureBlobStorage, AzureBlobStorage>();


                services.AddScoped<IInvoiceService, InvoiceService>();
                services.AddScoped<IMappingService, MappingService>();
                services.AddScoped<IFileMappingService, FileMappingService>();


                services.AddScoped<IReportService, ReportService>();

                services.AddScoped<IRoleService, RoleService>();

                services.AddScoped<IFlowService, FlowService>();

                services.AddScoped<IUserManagementSystemService, UserManagementSystemService>();
                services.AddScoped<ICustomFieldService, CustomFieldService>();



                services.AddScoped<IStripeConnectService, StripeConnectService>();
                services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
                services.AddScoped<IPromoCodeService, PromoCodeService>();



                services.AddScoped<IAzureWebAppService, AzureWebAppService>();


                services.AddScoped<IInviteService, InviteService>();


                services.AddScoped<ISpreadsheetService, SpreadsheetService>();

                services.AddScoped<IAnalyticsService, AnalyticsService>();

                services.AddScoped<IKanbanService, KanbanService>();



                //services.AddScoped<IGoogleHome, GoogleHome>();

                services.AddScoped<IReadMailService, ReadMailService>();


                services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();

                services.AddScoped<IMarketplaceService, MarketplaceService>();

                services.AddScoped<ISitemapService, SitemapService>();



                services.AddScoped<IAzureDocumentIntelligenceService, AzureDocumentIntelligenceService>(provider =>
                    ActivatorUtilities.CreateInstance<AzureDocumentIntelligenceService>(provider, "", "https://namehere.cognitiveservices.azure.com/")
                );



                services.AddSignalR((services) =>
                {
                    services.EnableDetailedErrors = true;
                });


                services.AddScoped<IFormRecognizerService, FormRecognizerService>(provider =>
                    ActivatorUtilities.CreateInstance<FormRecognizerService>(provider, "", "https://namehere.cognitiveservices.azure.com/")
                );



            }, (_appsettings, _currentEnvironment, services) =>
            {
                // Configure database with the provider specified in appsettings.json
                // Supports: SqlServer, PostgreSQL, MySQL, SQLite
                services.AddAuthScapeDatabase(
                    _appsettings,
                    enableSensitiveDataLogging: _currentEnvironment.IsDevelopment(),
                    useOpenIddict: true,
                    lifetime: ServiceLifetime.Scoped);
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            authenticationManager.Configure(app, env, (endpoints) =>
            {
                endpoints.MapHub<SpreadsheetHub>("/chat");
                endpoints.MapHub<PageBuilderHub>("/pagebuilder");
            });

            // remove if not using wwwroot folder...
            app.UseStaticFiles();
        }
    }
}