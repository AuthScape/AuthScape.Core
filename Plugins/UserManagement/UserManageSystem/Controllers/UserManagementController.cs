using AuthScape.UserManagementSystem.Models;
using AuthScape.UserManageSystem.Models;
using AuthScape.UserManageSystem.Services;
using CoreBackpack.Pagination;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using System.Globalization;

namespace AuthScape.UserManageSystem.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public class UserManagementController : ControllerBase
    {
        readonly IUserManagementSystemService userManagementSystemService;
        public UserManagementController(IUserManagementSystemService userManagementSystemService)
        {
            this.userManagementSystemService = userManagementSystemService;
        }

        [HttpPost]
        public async Task<IActionResult> GetUsers(GetUsersParam param)
        {
            var users = await userManagementSystemService.GetUsers(param.offset, param.length, param.searchByName, param.searchByCompanyId, param.searchByRoleId, param.IsActive);

            return Ok(new ReactDataTable()
            {
                draw = 0,
                recordsTotal = users.total,
                recordsFiltered = users.total,
                data = users.ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCompanies()
        {
            return Ok(await userManagementSystemService.GetAllCompanies());
        }

        [HttpPost]
        public async Task<IActionResult> GetCompanies(GetUsersParam param)
        {
            var users = await userManagementSystemService.GetCompanies(param.offset, param.length, param.searchByName, param.IsActive);

            return Ok(new ReactDataTable()
            {
                draw = 0,
                recordsTotal = users.total,
                recordsFiltered = users.total,
                data = users.ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCompaniesForLocation(string searchBName)
        {
            var companies = await userManagementSystemService.GetCompanies(0, 50, searchBName);
            return Ok(companies.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount(CreateUserParam param)
        {
            var userId = await userManagementSystemService.AddUser(param.FirstName, param.LastName, param.Email);
            return Ok(userId);
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            return Ok(await userManagementSystemService.GetAllRoles());
        }

        [HttpPost]
        public async Task<IActionResult> AddRole(AddRoleParam param)
        {
            await userManagementSystemService.AddRole(param.Role);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AssignUserToRole(AssignUserToRoleParam param)
        {
            await userManagementSystemService.AssignUserToRole(param.RoleId, param.UserId);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveUserFromRole(AssignUserToRoleParam param)
        {
            await userManagementSystemService.RemoveUserFromRole(param.RoleId, param.UserId);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetPermissions()
        {
            return Ok(await userManagementSystemService.GetPermissions());
        }

        [HttpPost]
        public async Task<IActionResult> AddPermission(AddPermissionParam param)
        {
            await userManagementSystemService.AddPermission(param.Name);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetUser(long userId)
        {
            return Ok(await userManagementSystemService.GetUser(userId));
        }

        [HttpGet]
        public async Task<IActionResult> GetCompany(long companyId)
        {
            return Ok(await userManagementSystemService.GetCompany(companyId));
        }

        [HttpPut]
        public async Task<IActionResult> UpdateUser(UserEditResult user)
        {
            var response = await userManagementSystemService.UpdateUser(user);
            return Ok(response);
        }

        [HttpDelete]
        public async Task<IActionResult> ArchiveUser(long id)
        {
            await userManagementSystemService.ArchiveUser(id);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> ArchiveLocation(long id)
        {
            await userManagementSystemService.ArchiveLocation(id);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> ArchiveCompany(long id)
        {
            await userManagementSystemService.ArchiveCompany(id);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> ActivateUser(long id)
        {
            await userManagementSystemService.ActivateUser(id);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> ActivateLocation(long id)
        {
            await userManagementSystemService.ActivateLocation(id);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> ActivateCompany(long id)
        {
            await userManagementSystemService.ActivateCompany(id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UploadUsers(UploadUsersParam param)
        {
            var accountsNotUploaded = new List<UserManagementUploadField>();

            var uploadFields = new List<UserManagementUploadField>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null
            };


            using (var reader = new StreamReader(param.File.OpenReadStream()))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new UserManagementUploadField
                    {
                        FirstName = csv.GetField<string>("FirstName"),
                        LastName = csv.GetField<string>("LastName"),
                        Email = csv.GetField<string>("Email")
                    };
                    record.Properties = new Dictionary<string, string>();

                    var password = csv.GetField<string?>("Password");
                    if (!String.IsNullOrWhiteSpace(password))
                    {
                        record.Password = password;
                    }

                    var companyId = csv.GetField<string?>("CompanyId");
                    if (!String.IsNullOrWhiteSpace(companyId))
                    {
                        record.CompanyId = companyId;
                    }

                    var phoneNumber = csv.GetField<string?>("PhoneNumber");
                    if (!String.IsNullOrWhiteSpace(phoneNumber))
                    {
                        record.PhoneNumber = phoneNumber;
                    }

                    var locationId = csv.GetField<string?>("LocationId");
                    if (!String.IsNullOrWhiteSpace(locationId))
                    {
                        record.LocationId = locationId;
                    }


                    // add your roles comma seperated
                    var roles = csv.GetField<string?>("Roles");
                    if (!String.IsNullOrWhiteSpace(roles))
                    {
                        record.Roles = roles;
                    }

                    var permissions = csv.GetField<string?>("Permissions");
                    if (!String.IsNullOrWhiteSpace(permissions))
                    {
                        record.Permissions = permissions;
                    }

                    // add the properties
                    var headerProperties = csv.HeaderRecord
                        .Where(h => h != "FirstName" && h != "LastName" && h != "Email" && h != "Password" && h != "PhoneNumber" &&
                        h != "CompanyId" && h != "LocationId" && h != "Roles" && h != "Permissions").ToList();

                    foreach (var headerProperty in headerProperties)
                    {
                        try
                        {
                            record.Properties.Add(headerProperty, csv.GetField<string>(headerProperty));
                        }
                        catch (Exception) { }
                    }

                    uploadFields.Add(record);
                }
            }

            foreach (var uploadField in uploadFields)
            {
                var isSuccessful = await userManagementSystemService.AddUser(
                    uploadField.FirstName,
                    uploadField.LastName,
                    uploadField.Email,
                    uploadField.PhoneNumber,
                    uploadField.Password,
                    !String.IsNullOrWhiteSpace(uploadField.CompanyId) ? Convert.ToInt64(uploadField.CompanyId) : null,
                    !String.IsNullOrWhiteSpace(uploadField.LocationId) ? Convert.ToInt64(uploadField.LocationId) : null,
                    uploadField.Roles,
                    uploadField.Permissions,
                    uploadField.Properties
                );

                if (isSuccessful != null)
                {
                    uploadField.Password = "";
                    accountsNotUploaded.Add(uploadField);
                }
            }



            return Ok(accountsNotUploaded);
        }

        [HttpGet]
        public async Task<IActionResult> GetDownloadTemplate(CustomFieldPlatformType platformType)
        {
            var memoryStream = await userManagementSystemService.GetDownloadTemplate(platformType);
            if (memoryStream != null)
            {
                return File(memoryStream, "text/csv");
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanies(string? name = null)
        {
            return Ok(await userManagementSystemService.GetCompanies(name));
        }

        [HttpGet]
        public async Task<IActionResult> GetLocations(long? companyId, string? name = null)
        {
            return Ok(await userManagementSystemService.GetLocationsList(new GetLocationParam()
            {
                Name = name,
                CompanyId = companyId,
            }));
        }

        [HttpPost]
        public async Task<IActionResult> GetLocations(GetLocationParam param)
        {
            var users = await userManagementSystemService.GetLocations(param);

            return Ok(new ReactDataTable()
            {
                draw = 0,
                recordsTotal = users.total,
                recordsFiltered = users.total,
                data = users.ToList()
            });
        }

        [HttpPut]
        public async Task<IActionResult> ChangeUserPassword(ChangeUserPasswordParam param)
        {
            var response = await userManagementSystemService.ChangeUserPassword(param.UserId, param.Password);
            return Ok(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomFields(CustomFieldPlatformType platformType, bool IsDatagrid = false)
        {
            var customFields = await userManagementSystemService.GetAllCustomFields(platformType, IsDatagrid);
            return Ok(customFields);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomTabs(CustomFieldPlatformType platformType)
        {
            return Ok(await userManagementSystemService.GetCustomTabs(platformType));
        }

        [HttpPost]
        public async Task<IActionResult> CreateTab(CreateTabParam param)
        {
            var tabId = await userManagementSystemService.CreateTab(param.Name, param.PlatformType);
            return Ok(tabId);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomField(Guid id)
        {
            return Ok(await userManagementSystemService.GetCustomField(id));
        }

        [HttpPost]
        public async Task<IActionResult> AddOrUpdateCustomField(CustomFieldParam customField)
        {
            await userManagementSystemService.AddUpdateCustomField(customField);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteCustomField(Guid id)
        {
            await userManagementSystemService.DeleteCustomField(id);
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteCustomTab(Guid id)
        {
            await userManagementSystemService.DeleteCustomTab(id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCompany(CompanyEditParam param)
        {
            var response = await userManagementSystemService.UpdateCompany(param);
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLocation(LocationEditParam param)
        {
            var response = await userManagementSystemService.UpdateLocation(param);
            return Ok(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetLocation(long locationId)
        {
            return Ok(await userManagementSystemService.GetLocation(locationId));
        }

        [HttpPost]
        public async Task<IActionResult> UploadLogo([FromForm] UserManagementCompanyLogo logo)
        {
            await userManagementSystemService.UploadLogo(logo);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UploadCustomFieldImage([FromForm] UserManagementCustomFieldImage param)
        {
            return Ok(await userManagementSystemService.UploadCustomFieldImage(param));
        }
    }


    public class CreateTabParam
    {
        public string Name { get; set; }
        public CustomFieldPlatformType PlatformType { get; set; }
    }

    public class ChangeUserPasswordParam
    {
        public long UserId { get; set; }
        public string Password { get; set; }
    }

    public class GetUsersParam
    {
        public int offset { get; set; }
        public int length { get; set; }
        public string? searchByName { get; set; }

        public long? searchByCompanyId { get; set; }
        public long? searchByRoleId { get; set; }

        public bool IsActive { get; set; }
    }

    public class GetLocationParam
    {
        public int offset { get; set; }
        public int length { get; set; }
        public string? Name { get; set; }
        public long? CompanyId { get; set; }
        public int CompanyType { get; set; }

        public bool IsActive { get; set; }
    }

    public class UserManagementUploadField
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? Password { get; set; } = null;
        public string? CompanyId { get; set; } = null;
        public string? LocationId { get; set; } = null;
        public string? PhoneNumber { get; set; } = null;
        public string? Roles { get; set; }
        public string? Permissions { get; set; }

        public Dictionary<string, string>? Properties { get; set; }
    }

    public class UserManagementUploadFieldProperty
    {
        public string Header { get; set; }
        public string? Value { get; set; }
    }


    public class UploadUsersParam
    {
        public IFormFile File { get; set; }
    }

    public class AddPermissionParam
    {
        public string Name { get; set; }
    }

    public class AssignUserToRoleParam
    {
        public long UserId { get; set; }
        public long RoleId { get; set; }
    }

    public class AddRoleParam
    {
        public string Role { get; set; }
    }
}
