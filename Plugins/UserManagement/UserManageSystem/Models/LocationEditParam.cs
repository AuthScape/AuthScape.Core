namespace AuthScape.UserManageSystem.Models
{
    public class LocationEditParam
    {
        public long Id { get; set; }
        public long CompanyId { get; set; }
        public string Title { get; set; }
        public bool IsDeactivated { get; set; }

        public List<CustomFieldResult> CustomFields { get; set; }
    }
}