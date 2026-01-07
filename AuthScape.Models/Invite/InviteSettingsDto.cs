namespace AuthScape.Models.Invite
{
    public class InviteSettingsDto
    {
        public Guid? Id { get; set; }
        public long? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public bool EnableInviteToCompany { get; set; }
        public bool EnableInviteToLocation { get; set; }
        public bool AllowSettingPermissions { get; set; }
        public bool AllowSettingRoles { get; set; }
        public bool EnforceSamePermissions { get; set; }
        public bool EnforceSameRole { get; set; }

        public bool IsGlobalDefault => !CompanyId.HasValue;
    }

    public class UpdateInviteSettingsDto
    {
        public long? CompanyId { get; set; }
        public bool EnableInviteToCompany { get; set; }
        public bool EnableInviteToLocation { get; set; }
        public bool AllowSettingPermissions { get; set; }
        public bool AllowSettingRoles { get; set; }
        public bool EnforceSamePermissions { get; set; }
        public bool EnforceSameRole { get; set; }
    }

    public class InviteValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public InviteSettingsDto? EffectiveSettings { get; set; }
    }
}
