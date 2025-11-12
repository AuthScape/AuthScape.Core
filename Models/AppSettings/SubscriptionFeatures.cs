namespace Models.AppSettings
{
    /// <summary>
    /// Feature flags for subscription functionality
    /// </summary>
    public class SubscriptionFeatures
    {
        /// <summary>
        /// Enable user-level subscriptions (each user has their own subscription)
        /// </summary>
        public bool EnableUserSubscriptions { get; set; } = true;

        /// <summary>
        /// Enable company-level subscriptions (one subscription for the entire company)
        /// </summary>
        public bool EnableCompanySubscriptions { get; set; } = false;

        /// <summary>
        /// Enable location-level subscriptions
        /// </summary>
        public bool EnableLocationSubscriptions { get; set; } = false;

        /// <summary>
        /// Allow users to have multiple active subscriptions simultaneously
        /// </summary>
        public bool AllowMultipleSubscriptions { get; set; } = false;

        /// <summary>
        /// Enable trial periods for subscriptions
        /// </summary>
        public bool EnableTrialPeriods { get; set; } = true;

        /// <summary>
        /// Default trial period in days (if trials are enabled)
        /// </summary>
        public int DefaultTrialDays { get; set; } = 14;

        /// <summary>
        /// Enable promo codes and coupons
        /// </summary>
        public bool EnablePromoCodes { get; set; } = true;

        /// <summary>
        /// Enable subscription upgrades
        /// </summary>
        public bool EnableUpgrades { get; set; } = true;

        /// <summary>
        /// Enable subscription downgrades
        /// </summary>
        public bool EnableDowngrades { get; set; } = true;

        /// <summary>
        /// Enable pausing subscriptions
        /// </summary>
        public bool EnablePauseResume { get; set; } = true;

        /// <summary>
        /// Automatically retry failed payments
        /// </summary>
        public bool AutoRetryFailedPayments { get; set; } = true;

        /// <summary>
        /// Number of days to retry failed payments before canceling
        /// </summary>
        public int PaymentRetryDays { get; set; } = 7;

        /// <summary>
        /// Send email notifications for subscription events
        /// </summary>
        public bool EnableEmailNotifications { get; set; } = true;

        /// <summary>
        /// Notify users X days before trial ends
        /// </summary>
        public int TrialEndingNotificationDays { get; set; } = 3;

        /// <summary>
        /// Enable usage-based billing
        /// </summary>
        public bool EnableUsageBasedBilling { get; set; } = false;

        /// <summary>
        /// Enable metered billing
        /// </summary>
        public bool EnableMeteredBilling { get; set; } = false;

        /// <summary>
        /// Require payment method before starting trial
        /// </summary>
        public bool RequirePaymentMethodForTrial { get; set; } = true;

        /// <summary>
        /// Allow immediate cancellation (vs cancel at period end)
        /// </summary>
        public bool AllowImmediateCancellation { get; set; } = false;

        /// <summary>
        /// Enable proration for upgrades/downgrades
        /// </summary>
        public bool EnableProration { get; set; } = true;

        /// <summary>
        /// Show billing history to users
        /// </summary>
        public bool ShowBillingHistory { get; set; } = true;

        /// <summary>
        /// Allow users to download invoices as PDF
        /// </summary>
        public bool AllowInvoiceDownload { get; set; } = true;

        /// <summary>
        /// Enable ACH payment methods for subscriptions
        /// </summary>
        public bool EnableACHPayments { get; set; } = true;

        /// <summary>
        /// Minimum amount in cents for ACH payments
        /// </summary>
        public int MinimumACHAmount { get; set; } = 500; // $5.00

        /// <summary>
        /// Enable card payment methods for subscriptions
        /// </summary>
        public bool EnableCardPayments { get; set; } = true;
    }
}
