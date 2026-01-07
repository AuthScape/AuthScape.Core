using AuthScape.Models.Users;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.PaymentGateway.Plans
{
    /// <summary>
    /// Join table linking SubscriptionPlans to allowed Roles.
    /// If a plan has no entries here, it's available to all roles.
    /// </summary>
    public class SubscriptionPlanRole
    {
        public Guid SubscriptionPlanId { get; set; }
        public long RoleId { get; set; }

        // Navigation properties
        [ForeignKey(nameof(SubscriptionPlanId))]
        public virtual SubscriptionPlan SubscriptionPlan { get; set; } = null!;

        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
