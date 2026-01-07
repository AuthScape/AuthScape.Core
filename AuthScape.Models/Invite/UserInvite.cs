namespace AuthScape.Models.Invite
{
    public enum InviteStatus
    {
        Pending = 0,
        Accepted = 1,
        Expired = 2,
        Cancelled = 3
    }

    public class UserInvite
    {
        public Guid Id { get; set; }

        /// <summary>
        /// The user being invited
        /// </summary>
        public long InvitedUserId { get; set; }

        /// <summary>
        /// The user who sent the invite
        /// </summary>
        public long InviterId { get; set; }

        /// <summary>
        /// Password reset token for invite acceptance
        /// </summary>
        public string? InviteToken { get; set; }

        /// <summary>
        /// Company the user is being invited to
        /// </summary>
        public long? CompanyId { get; set; }

        /// <summary>
        /// Location the user is being invited to
        /// </summary>
        public long? LocationId { get; set; }

        /// <summary>
        /// JSON array of role IDs to assign when invite is accepted
        /// </summary>
        public string? AssignedRoles { get; set; }

        /// <summary>
        /// JSON array of permission GUIDs to assign when invite is accepted
        /// </summary>
        public string? AssignedPermissions { get; set; }

        public InviteStatus Status { get; set; } = InviteStatus.Pending;

        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
