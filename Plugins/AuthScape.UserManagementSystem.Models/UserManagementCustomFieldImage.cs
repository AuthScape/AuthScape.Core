using AuthScape.UserManagementSystem.Models;
using Microsoft.AspNetCore.Http;

namespace AuthScape.UserManageSystem.Models
{
    public class UserManagementCustomFieldImage
    {
        public IFormFile File { get; set; }
        public CustomFieldPlatformType PlatformType { get; set; }
        public long Identifier { get; set; }
        public Guid CustomFieldId { get; set; }
    }
}
