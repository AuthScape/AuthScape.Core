using AuthScape.Models.Users;
using AuthScape.SendGrid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Email;
using Services.Context;
using Services.Database;

namespace AuthScape.Services
{
    public interface IAchVerificationEmailService
    {
        Task SendInitialVerificationEmailAsync(
            long userId,
            string bankLast4,
            string bankName,
            string arrivalDate,
            string hostedVerificationUrl,
            string verificationType);

        Task SendReminderEmailAsync(
            string stripeCustomerId,
            string bankLast4,
            string bankName,
            string hostedVerificationUrl,
            string verificationType);
    }

    public class AchVerificationEmailService : IAchVerificationEmailService
    {
        private readonly ISendGridService _sendGridService;
        private readonly DatabaseContext _db;
        private readonly AppSettings _appSettings;

        public AchVerificationEmailService(
            ISendGridService sendGridService,
            DatabaseContext db,
            IOptions<AppSettings> appSettings)
        {
            _sendGridService = sendGridService;
            _db = db;
            _appSettings = appSettings.Value;
        }

        public async Task SendInitialVerificationEmailAsync(
            long userId,
            string bankLast4,
            string bankName,
            string arrivalDate,
            string hostedVerificationUrl,
            string verificationType)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || string.IsNullOrEmpty(user.Email)) return;

            var templateId = _appSettings.SendGrid?.AchVerificationInitialTemplateId;
            if (string.IsNullOrEmpty(templateId)) return;

            var verificationLink = BuildIdpVerificationLink();

            var emailModel = new AchVerificationEmail
            {
                Email = user.Email,
                FirstName = user.FirstName ?? "Customer",
                BankLast4 = bankLast4 ?? "****",
                BankName = bankName ?? "your bank",
                ArrivalDate = arrivalDate ?? "1-2 business days",
                VerificationLink = verificationLink,
                HostedVerificationUrl = hostedVerificationUrl ?? verificationLink,
                VerificationType = verificationType ?? "amounts",
                EmailType = "initial",
                AppName = _appSettings.Name ?? "Our Service"
            };

            await _sendGridService.Send(
                new AppUser { Email = user.Email, FirstName = user.FirstName, IsActive = true },
                templateId,
                emailModel,
                subject: "Verify Your Bank Account",
                allowIfNotActive: true);
        }

        public async Task SendReminderEmailAsync(
            string stripeCustomerId,
            string bankLast4,
            string bankName,
            string hostedVerificationUrl,
            string verificationType)
        {
            if (string.IsNullOrEmpty(stripeCustomerId)) return;

            var wallet = await _db.Wallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.PaymentCustomerId == stripeCustomerId);

            if (wallet?.User == null || string.IsNullOrEmpty(wallet.User.Email)) return;

            var templateId = _appSettings.SendGrid?.AchVerificationReminderTemplateId;
            if (string.IsNullOrEmpty(templateId)) return;

            var user = wallet.User;
            var verificationLink = BuildIdpVerificationLink();

            var emailModel = new AchVerificationEmail
            {
                Email = user.Email,
                FirstName = user.FirstName ?? "Customer",
                BankLast4 = bankLast4 ?? "****",
                BankName = bankName ?? "your bank",
                ArrivalDate = "now available",
                VerificationLink = verificationLink,
                HostedVerificationUrl = hostedVerificationUrl ?? verificationLink,
                VerificationType = verificationType ?? "amounts",
                EmailType = "reminder",
                AppName = _appSettings.Name ?? "Our Service"
            };

            await _sendGridService.Send(
                new AppUser { Email = user.Email, FirstName = user.FirstName, IsActive = true },
                templateId,
                emailModel,
                subject: "Your Micro-Deposits Have Arrived - Complete Verification",
                allowIfNotActive: true);
        }

        private string BuildIdpVerificationLink()
        {
            var baseUrl = _appSettings.IDPUrl?.TrimEnd('/') ?? "";
            return $"{baseUrl}/Identity/Account/Manage/Payments?tab=payment-methods";
        }
    }
}
