using Microsoft.AspNetCore.Http;

namespace AuthScape.UserManageSystem.Models
{
    public class UserManagementCompanyLogo
    {
        public long CompanyId { get; set; }
        public IFormFile File { get; set; }
    }
}
