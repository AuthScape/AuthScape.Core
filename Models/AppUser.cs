using AuthScape.Models.PaymentGateway;
using AuthScape.UserManageSystem.Models;
using Microsoft.AspNetCore.Identity;
using Models.Authentication;
using Models.Users;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.Users
{
    public class AppUser : IdentityUser<long>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? locale { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Archived { get; set; }
        public DateTimeOffset LastLoggedIn { get; set; }
        public bool IsActive { get; set; }
        public string? PhotoUri { get; set; }
        public long? CompanyId { get; set; }
        public long? LocationId { get; set; }
        public DateTimeOffset? WhenInviteSent { get; set; }
        public Location? Location { get; set; }
        public Company? Company { get; set; }
        public ICollection<Wallet> Cards { get; set; }
        public ICollection<StoreCredit> StoreCredits { get; set; }
        public ICollection<StoreCredit> GiftedCredit { get; set; } // credits that you gifted to another user

        public ICollection<UserLocations> UserLocations { get; set; }

        public virtual ICollection<Fido2Credential> Credentials { get; set; }


        [NotMapped]
        public string? Permissions { get; set; }
        [NotMapped]
        public string? Roles { get; set; }
        [NotMapped]
        public List<CustomFieldResult> CustomFields { get; set; }

    }
}