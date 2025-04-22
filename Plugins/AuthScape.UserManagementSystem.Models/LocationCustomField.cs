namespace AuthScape.UserManagementSystem.Models
{
    public class LocationCustomField
    {
        public long LocationId { get; set; }
        public Guid CustomFieldId { get; set; }
        public string Value { get; set; }

        public CustomField CustomField { get; set; }
    }
}