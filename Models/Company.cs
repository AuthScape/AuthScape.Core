using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.Users
{
    public class Company
    {
        public long Id { get; set; }
        public string? Logo { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }

        public bool IsDeactivated { get; set; }

        public ICollection<AppUser> Users { get; set; }
        public ICollection<Location> Locations { get; set; }

        [NotMapped]
        public List<string> EmailDomains { get; set; }
    }

    public class NCACompanyQuery
    {
        public string label { get; set; }
        public long id { get; set; }
    }
}
