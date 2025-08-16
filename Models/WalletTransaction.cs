using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthScape.Models.PaymentGateway
{
    public class WalletTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WalletId { get; set; }
        public Wallet Wallet { get; set; } = default!;

        public decimal Amount { get; set; }          // +credit, -debit
        public string Currency { get; set; } = "usd";

        public string? StripeObjectId { get; set; }
        public string? ExternalRef { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public string? CreatedByUserId { get; set; }
    }
}
