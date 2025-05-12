namespace AuthScape.UserManageSystem.Models
{
    public class LocationEditParam
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public bool IsDeactivated { get; set; }


        public long? CompanyId { get; set; }

        public List<CustomFieldResult> CustomFields { get; set; }
    }
}