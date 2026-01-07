namespace AuthScape.Models.Invite
{
    public class InviteSettings
    {
        public Guid Id { get; set; }

        /// <summary>
        /// NULL = global default settings, non-NULL = company-specific override
        /// </summary>
        public long? CompanyId { get; set; }

        /// <summary>
        /// Allow inviting users to a company
        /// </summary>
        public bool EnableInviteToCompany { get; set; } = true;

        /// <summary>
        /// Allow inviting users to a location
        /// </summary>
        public bool EnableInviteToLocation { get; set; } = true;

        /// <summary>
        /// Allow inviter to assign permissions to new users
        /// </summary>
        public bool AllowSettingPermissions { get; set; } = true;

        /// <summary>
        /// Allow inviter to assign roles to new users
        /// </summary>
        public bool AllowSettingRoles { get; set; } = true;

        /// <summary>
        /// Inviter can only assign permissions they themselves have
        /// </summary>
        public bool EnforceSamePermissions { get; set; } = false;

        /// <summary>
        /// Inviter can only assign roles they themselves have
        /// </summary>
        public bool EnforceSameRole { get; set; } = false;

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}
