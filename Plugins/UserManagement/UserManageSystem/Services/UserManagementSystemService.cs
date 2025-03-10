﻿using AuthScape.Models.Users;
using AuthScape.Services;
using AuthScape.UserManageSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models.Users;
using Services.Context;
using CoreBackpack;
using AuthScape.Models.Exceptions;
using AuthScape.UserManagementSystem.Models;
using StrongGrid.Resources;
using CoreBackpack.Time;
using Newtonsoft.Json;
using System.Text;

namespace AuthScape.UserManageSystem.Services
{
    public interface IUserManagementSystemService
    {
        Task<List<Role>> GetAllRoles();
        Task AddRole(string roleName);
        Task AssignUserToRole(long roleId, long userId);
        Task RemoveUserFromRole(long roleId, long userId);
        Task<List<IdentityUserClaim<long>>> GetClaims(long userId);
        Task<PagedList<AppUser>> GetUsers(int offset, int length, string? searchByName = null, long? searchByCompanyId = null, long? searchByRoleId = null);
        Task AddPermission(string permissionName);
        Task<List<Permission>> GetPermissions();
        Task<UserEditResult?> GetUser(long userId);
        Task UpdateUser(UserEditResult user);
        Task<bool> AddUser(string firstName, string lastName, string email, string? phoneNumber = null, string? password = null, long? companyId = null, long? locationId = null, string? Roles = null, string? Permissions = null, Dictionary<string, string>? additionalFields = null);
        Task<List<Company>> GetCompanies(string? name = null);
        Task<List<Location>> GetLocations(long companyId, string? name = null);
        Task<string> ChangeUserPassword(long userId, string newPassword);
        Task<List<CustomField>> GetAllCustomFields(CustomFieldPlatformType platformType);
        Task<PagedList<CompanyDataGrid>> GetCompanies(int offset, int length, string? searchByName = null);
        Task<Company> GetCompany(long companyId);
        Task UpdateCompany(CompanyEditParam param);
        Task<CustomField?> GetCustomField(Guid id);
        Task AddUpdateCustomField(CustomFieldParam param);
        Task ArchiveUser(long id);

        Task DeleteCustomField(Guid id);
        Task DeleteCustomTab(Guid id);
        Task<List<CustomFieldTab>> GetCustomTabs(CustomFieldPlatformType platformType);
        Task<Guid> CreateTab(string name, CustomFieldPlatformType platformType);
        Task CreateUserAccount(string FirstName, string LastName, string Email);
        Task<List<CompanyDataGrid>> GetAllCompanies();
        Task<MemoryStream?> GetDownloadTemplate(CustomFieldPlatformType platformType);
    }

    public class UserManagementSystemService : IUserManagementSystemService
    {
        readonly DatabaseContext databaseContext;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        readonly IUserManagementService userManagementService;

        public UserManagementSystemService(DatabaseContext databaseContext, SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, IUserManagementService userManagementService)
        {
            this.databaseContext = databaseContext;

            _userManager = userManager;
            _signInManager = signInManager;
            this.userManagementService = userManagementService;
        }

        public async Task<List<Role>> GetAllRoles()
        {
            return await databaseContext.Roles.ToListAsync();
        }

        public async Task AddRole(string roleName)
        {
            await databaseContext.Roles.AddAsync(new Role()
            {
                Name = roleName,
                NormalizedName = roleName.ToLower(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            });
            await databaseContext.SaveChangesAsync();
        }

        public async Task ArchiveUser(long id)
        {
            var user = await databaseContext.Users.Where(u => u.Id == id).FirstOrDefaultAsync();
            if (user != null)
            {
                user.IsActive = false;
                user.Archived = DateTimeOffset.Now;
                await databaseContext.SaveChangesAsync();
            }
        }
        public async Task AssignUserToRole(long roleId, long userId)
        {
            await databaseContext.UserRoles.AddAsync(new Microsoft.AspNetCore.Identity.IdentityUserRole<long>()
            {
                RoleId = roleId,
                UserId = userId
            });
            await databaseContext.SaveChangesAsync();
        }

        public async Task RemoveUserFromRole(long roleId, long userId)
        {
            var userRole = await databaseContext.UserRoles.Where(u => u.UserId == userId && u.RoleId == roleId).FirstOrDefaultAsync();
            if (userRole != null)
            {
                databaseContext.UserRoles.Remove(userRole);
                await databaseContext.SaveChangesAsync();
            }
        }

        public async Task<List<IdentityUserClaim<long>>> GetClaims(long userId)
        {
            return await databaseContext.UserClaims.Where(c => c.UserId == userId).ToListAsync();
        }

        public async Task AddUpdateCustomField(CustomFieldParam param)
        {
            if (param.Id == null)
            {
                await databaseContext.CustomFields.AddAsync(new CustomField()
                {
                    Name = param.Name,
                    CustomFieldPlatformType = param.CustomFieldPlatformType,
                    FieldType = param.FieldType,
                    GridSize = param.GridSize,
                    IsRequired = param.IsRequired,
                    TabId = param.TabSelection
                });
            }
            else
            {
                var customField = await databaseContext.CustomFields.Where(c => c.Id == param.Id).FirstOrDefaultAsync();
                if (customField != null)
                {
                    customField.Name = param.Name;
                    customField.CustomFieldPlatformType = param.CustomFieldPlatformType;
                    customField.FieldType = param.FieldType;
                    customField.GridSize = param.GridSize;
                    customField.IsRequired = param.IsRequired;
                    customField.TabId = param.TabSelection;
                }
            }

            await databaseContext.SaveChangesAsync();
        }

        public async Task<List<CompanyDataGrid>> GetAllCompanies()
        {
            var companyQuery = databaseContext.Companies
                .Include(c => c.Users)
                .Include(c => c.Locations)
                .Where(c => !c.IsDeactivated)
                .Select(c => new CompanyDataGrid()
                {
                    Id = c.Id,
                    Logo = c.Logo,
                    Title = c.Title,
                    NumberOfLocations = c.Locations.Count(),
                    NumberOfUsers = c.Users.Count()
                });

            return await companyQuery.ToListAsync();
        }

        public async Task<PagedList<CompanyDataGrid>> GetCompanies(int offset, int length, string? searchByName = null)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            var companyQuery = databaseContext.Companies
                .Include(c => c.Users)
                .Include(c => c.Locations)
                .Where(c => !c.IsDeactivated)
                .Select(c => new CompanyDataGrid() {
                Id = c.Id,
                Logo = c.Logo,
                Title = c.Title,
                NumberOfLocations = c.Locations.Count(),
                NumberOfUsers = c.Users.Count()
            });


            if (!String.IsNullOrWhiteSpace(searchByName))
            {
                searchByName = searchByName.ToLower();
                companyQuery = companyQuery.Where(u => u.Title.ToLower().Contains(searchByName));
            }

            var companies = await companyQuery
                .OrderBy(c => c.Title)
                .ToPagedResultAsync(offset, length);

            return companies;
        }

        public async Task<PagedList<AppUser>> GetUsers(int offset, int length, string? searchByName = null, long? searchByCompanyId = null, long? searchByRoleId = null)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            var usersQuery = databaseContext.Users
                .AsNoTracking()
                .Include(u => u.Company)
                .AsQueryable();
                
            if (searchByCompanyId != null)
            {
                usersQuery = usersQuery.Where(u => u.CompanyId == searchByCompanyId.Value);
            }

            if (searchByRoleId != null)
            {
                usersQuery = usersQuery.Where(z => databaseContext.UserRoles.Where(u => u.RoleId == searchByRoleId.Value && u.UserId == z.Id).Any());
            }


            if (!String.IsNullOrWhiteSpace(searchByName))
            {
                searchByName = searchByName.ToLower();
                usersQuery = usersQuery.Where(u => 
                    u.UserName.ToLower().Contains(searchByName) || 
                    (u.FirstName + " " + u.LastName).ToLower().Contains(searchByName));
            }


            usersQuery = usersQuery.Select(u => new AppUser()
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                UserName = u.UserName,
                IsActive = u.IsActive,
                Archived = u.Archived,
                PhoneNumber = u.PhoneNumber,
                Company = new Company()
                {
                    Title = u.Company != null ? u.Company.Title : ""
                },
                Location = new Location()
                {
                    Title = u.Location != null ? u.Location.Title : ""
                }
            });

            var users = await usersQuery
                .OrderBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .ToPagedResultAsync(offset, length);

            var userIds = users.Select(u => u.Id).ToHashSet();

            var userRoles = await databaseContext.UserRoles
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.UserId))
                    .GroupBy(u => u.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.Select(u => u.RoleId).ToList());

            var roleNames = await databaseContext.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name);

            //var permissions = new List<string>();

            var userClaims = await databaseContext.UserClaims
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId) && u.ClaimType == "permissions")
                .ToDictionaryAsync(u => u.UserId, u => !string.IsNullOrWhiteSpace(u.ClaimValue) ? u.ClaimValue.Split(",").Select(v => Guid.Parse(v)).ToList() : null);

            var claimValues = userClaims.Values.Where(v => v != null).SelectMany(l => l).ToHashSet();

            var userPermissions = await databaseContext.Permissions
                .AsNoTracking()
                .Where(p => claimValues.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            //if (!String.IsNullOrWhiteSpace(usrClaim))
            //{
            //    var allClaims = usrClaim.Split(",");
            //    foreach (var allClaim in allClaims)
            //    {
            //        var _permission = await databaseContext.Permissions.Where(p => p.Id == Guid.Parse(allClaim)).Select(s => s.Name).FirstOrDefaultAsync();
            //        if (!String.IsNullOrWhiteSpace(_permission))
            //        {
            //            permissions.Add(_permission);
            //        }
            //    }
            //}

            var userCustomFields = await databaseContext.UserCustomFields.AsNoTracking()
                .Include(cf => cf.CustomField)
                .Where(cf => userIds.Contains(cf.UserId))
                .GroupBy(cf => cf.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(cf => new CustomFieldResult
                {
                    CustomFieldId = cf.CustomFieldId,
                    CustomFieldType = cf.CustomField.FieldType,
                    IsRequired = cf.CustomField.IsRequired,
                    Name = cf.CustomField.Name,
                    Size = cf.CustomField.GridSize,
                    TabId = cf.CustomField.TabId,
                    Value = cf.Value
                }).ToList());

            foreach (var user in users)
            {

                if (userRoles != null && userRoles.ContainsKey(user.Id)) user.Roles = String.Join(",", userRoles[user.Id].Select(roleId => roleNames[roleId]));
                if (userClaims != null && userClaims.ContainsKey(user.Id)) user.Permissions = String.Join(",", userClaims[user.Id].Select(cv => userPermissions[cv]));
                if (userCustomFields != null && userCustomFields.ContainsKey(user.Id)) user.CustomFields = userCustomFields[user.Id];

                //var userRoles = await databaseContext.UserRoles
                //    .AsNoTracking()
                //    .Where(u => u.UserId == user.Id)
                //    .Select(u => u.RoleId)
                //    .ToListAsync();

                //foreach (var usrRoleId in userRoles)
                //{
                //    var role = await databaseContext.Roles
                //        .AsNoTracking()
                //        .Where(c => c.Id == usrRoleId)
                //        .Select(z => z.Name)
                //        .FirstOrDefaultAsync();

                //    if (role != null)
                //    {
                //        roles.Add(role);
                //    }
                //}

                //var permissions = new List<string>();
                //var usrClaim = await databaseContext.UserClaims
                //    .AsNoTracking()
                //    .Where(u => u.UserId == user.Id && u.ClaimType == "permissions")
                //    .Select(s => s.ClaimValue)
                //    .FirstOrDefaultAsync();

                //if (!String.IsNullOrWhiteSpace(usrClaim))
                //{
                //    var allClaims = usrClaim.Split(",");
                //    foreach (var allClaim in allClaims)
                //    {
                //        var _permission = await databaseContext.Permissions.Where(p => p.Id == Guid.Parse(allClaim)).Select(s => s.Name).FirstOrDefaultAsync();
                //        if (!String.IsNullOrWhiteSpace(_permission))
                //        {
                //            permissions.Add(_permission);
                //        }
                //    }
                //}

                //user.Roles = String.Join(",", roles);
                //user.Permissions = String.Join(",", permissions);
            }

            return users;
        }

        public async Task AddPermission(string permissionName)
        {
            await databaseContext.Permissions.AddAsync(new Permission()
            {
                Name = permissionName,
            });
            await databaseContext.SaveChangesAsync();
        }

        public async Task<List<Permission>> GetPermissions()
        {
            return await databaseContext.Permissions.ToListAsync();
        }

        public async Task<MemoryStream?> GetDownloadTemplate(CustomFieldPlatformType platformType)
        {
            var arrayCustomFields = await databaseContext.CustomFields
                .AsNoTracking().Where(cf => cf.CustomFieldPlatformType == platformType).ToListAsync();

            var csv = new StringBuilder();
            csv.Append("FirstName,LastName,Email,Password,CompanyId,PhoneNumber,Roles,Permissions");

            if (arrayCustomFields != null && arrayCustomFields.Count() > 0)
            {
                var nameArray = arrayCustomFields.Select(p => p.Name).ToList();

                if (nameArray.Count() > 0)
                {
                    csv.Append(",");
                }

                csv.Append(String.Join(",", nameArray));

                byte[] byteArray = Encoding.UTF8.GetBytes(csv.ToString());

                var memoryStream = new MemoryStream(byteArray);

                memoryStream.Seek(0, SeekOrigin.Begin);

                return memoryStream;
            }

            return null;
        }

        public async Task<Company> GetCompany(long companyId)
        {
            var customFields = await databaseContext.CustomFields.AsNoTracking()
                .Where(c => c.CustomFieldPlatformType == CustomFieldPlatformType.Companies)
                .Select(c => new CustomFieldResult()
                {
                    CustomFieldId = c.Id,
                    Name = c.Name,
                    IsRequired = c.IsRequired,
                    CustomFieldType = c.FieldType,
                    TabId = c.TabId,
                    Size = c.GridSize,
                    Value = ""
                }).ToListAsync();

            foreach (var field in customFields)
            {
                field.Value =
                    await databaseContext.CompanyCustomFields
                        .Where(c => c.CompanyId == companyId && c.CustomFieldId == field.CustomFieldId).Select(s => s.Value)
                        .AsNoTracking()
                        .FirstOrDefaultAsync();
            }

            var company = await databaseContext.Companies
                .Where(c => c.Id == companyId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            company.CustomFields = customFields;

            return company;
        }

        public async Task<UserEditResult?> GetUser(long userId)
        {
            var userCustomFields = new List<CustomFieldResult>();

            var user = await databaseContext.Users
                .Include(u => u.Company)
                .Include(u => u.Location)
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();

            // get selected roles
            var manageUserRole = new List<string>();
            var userRoles = await databaseContext.UserRoles.Where(u => u.UserId == user.Id).ToListAsync();
            foreach (var userRole in userRoles) 
            {
                var role = await databaseContext.Roles.Where(r => r.Id == userRole.RoleId).FirstOrDefaultAsync();
                if (role != null)
                {
                    manageUserRole.Add(role.Name);
                }
            }

            // get selected permissions
            var manageUserPermissions = new List<string>();
            var userPermission = await databaseContext.UserClaims.Where(u => u.UserId == user.Id && u.ClaimType == "permissions").FirstOrDefaultAsync();
            if (userPermission != null && !String.IsNullOrWhiteSpace(userPermission.ClaimType) && !String.IsNullOrWhiteSpace(userPermission.ClaimValue))
            {
                var selectedPermissions = userPermission.ClaimValue.Split(",");
                foreach (var selectedPermission in selectedPermissions)
                {
                    var role = await databaseContext.Permissions.Where(r => r.Id == Guid.Parse(selectedPermission)).FirstOrDefaultAsync();
                    if (role != null)
                    {
                        manageUserPermissions.Add(role.Name);
                    }
                }
            }

            userCustomFields = await databaseContext.CustomFields.AsNoTracking()
                .Where(c => c.CustomFieldPlatformType == CustomFieldPlatformType.Users)
                .Select(c => new CustomFieldResult()
            {
                CustomFieldId = c.Id,
                Name = c.Name,
                IsRequired = c.IsRequired,
                Size = c.GridSize,
                CustomFieldType = c.FieldType,
                TabId = c.TabId,
                Value = ""
            }).ToListAsync();

            foreach (var field in userCustomFields)
            {
                field.Value = 
                await databaseContext.UserCustomFields
                    .Where(c => c.UserId == userId && c.CustomFieldId == field.CustomFieldId).Select(s => s.Value)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            }

            return new UserEditResult()
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                CompanyId = user.CompanyId,
                LocationId = user.LocationId,
                Email = user.UserName,
                IsActive = user.IsActive,
                Roles = manageUserRole,
                Permissions = manageUserPermissions,
                CustomFields = userCustomFields,

                Company = user.Company != null ? new Company() {
                    Id = user.Company.Id,
                    Title = user.Company.Title,
                } : null,
                Location = user.Location != null ? new Location()
                {
                    Id = user.Location.Id,
                    Title = user.Location.Title
                } : null
            };
        }

        public async Task UpdateUser(UserEditResult user)
        {
            if (user == null)
            {
                return;
            }

            var usr = new AppUser();

            if (user.Id != -1)
            {
                usr = await databaseContext.Users
                    .Include(u => u.Company)
                    .Where(u => u.Id == user.Id)
                    .FirstOrDefaultAsync();
            }


            if (usr != null)
            {
                usr.FirstName = user.FirstName;
                usr.LastName = user.LastName;
                usr.UserName = user.Email;
                usr.Email = user.Email;
                usr.NormalizedEmail = user.Email.ToUpper();
                usr.NormalizedUserName = user.Email.ToUpper();
                usr.IsActive = user.IsActive;
                usr.PhoneNumber = user.PhoneNumber;
                usr.CompanyId = user.CompanyId;
                usr.LocationId = user.LocationId;


                if (String.IsNullOrWhiteSpace(usr.SecurityStamp))
                {
                    usr.SecurityStamp = Guid.NewGuid().ToString("D");
                }

                if (user.Id == -1)
                {
                    await databaseContext.Users.AddAsync(usr);
                    await databaseContext.SaveChangesAsync();
                }


                databaseContext.UserRoles.RemoveRange(databaseContext.UserRoles.Where(d => d.UserId == usr.Id));
                await databaseContext.SaveChangesAsync();

                // roles
                if (user.Roles != null)
                {
                    foreach (var role in user.Roles)
                    {
                        var roleItem = await databaseContext.Roles.AsNoTracking().Where(r => r.Name.ToLower() == role.ToLower()).FirstOrDefaultAsync();

                        var usrRole = await databaseContext.UserRoles
                            .Where(r => r.UserId == usr.Id &&
                                r.RoleId == roleItem.Id
                                ).FirstOrDefaultAsync();

                        if (usrRole == null)
                        {
                            var newUserRole = new IdentityUserRole<long>();
                            newUserRole.UserId = usr.Id;
                            newUserRole.RoleId = roleItem.Id;

                            await databaseContext.UserRoles.AddAsync(newUserRole);
                        }
                    }
                }
                else
                {
                    // no roles assigned to the user
                    var usrRoles = databaseContext.UserRoles.Where(r => r.UserId == usr.Id);
                    databaseContext.UserRoles.RemoveRange(usrRoles);
                }


                // permissions
                if (user.Permissions != null)
                {
                    List<string> permissionSelections = new List<string>();
                    foreach (var permission in user.Permissions)
                    {
                        var claimItem = await databaseContext.Permissions.AsNoTracking().Where(r => r.Name.ToLower() == permission.ToLower()).FirstOrDefaultAsync();
                        permissionSelections.Add(claimItem.Id.ToString());
                    }

                    var usrClaim = await databaseContext.UserClaims.Where(u => u.UserId == usr.Id && u.ClaimType == "permissions").FirstOrDefaultAsync();
                    if (usrClaim == null)
                    {
                        var newUserRole = new IdentityUserClaim<long>();
                        newUserRole.UserId = usr.Id;
                        newUserRole.ClaimType = "permissions";
                        newUserRole.ClaimValue = String.Join(",", permissionSelections);

                        await databaseContext.UserClaims.AddAsync(newUserRole);
                    }
                    else
                    {
                        usrClaim.ClaimValue = String.Join(",", permissionSelections);
                    }
                }
                else
                {
                    // no permissions assigned the user
                    var usrClaim = await databaseContext.UserClaims.Where(u => u.UserId == usr.Id && u.ClaimType == "permissions").FirstOrDefaultAsync();
                    if (usrClaim != null)
                    {
                        databaseContext.UserClaims.Remove(usrClaim);
                    }
                }



                // new custom field logic here....
                foreach (var customField in user.CustomFields)
                {
                    var userCustomField = await databaseContext.UserCustomFields
                        .Where(c => c.CustomFieldId == customField.CustomFieldId && c.UserId == usr.Id)
                        .FirstOrDefaultAsync();

                    if (userCustomField != null)
                    {
                        userCustomField.Value = customField.Value;
                    }
                    else
                    {
                        await databaseContext.UserCustomFields.AddAsync(new UserManagementSystem.Models.UserCustomField()
                        {
                            CustomFieldId = customField.CustomFieldId,
                            UserId = usr.Id,
                            Value = customField.Value
                        });
                    }
                }

                await databaseContext.SaveChangesAsync();


                // custom fields
                //foreach (var customField in user.UserCustomFields)
                //{
                //    var usrClaim = await databaseContext.UserClaims.Where(u => u.UserId == usr.Id && u.ClaimType == customField.Name).FirstOrDefaultAsync();
                //    if (usrClaim != null)
                //    {
                //        usrClaim.ClaimValue = customField.Value;
                //    }
                //    else
                //    {
                //        var newUserRole = new IdentityUserClaim<long>();
                //        newUserRole.UserId = usr.Id;
                //        newUserRole.ClaimType = customField.Name;
                //        newUserRole.ClaimValue = customField.Value;

                //        await databaseContext.UserClaims.AddAsync(newUserRole);
                //    }
                //}



                await databaseContext.SaveChangesAsync();
            }
        }

        public async Task<bool> AddUser(string firstName, string lastName, string email, string? phoneNumber = null,
            string? password = null, long? companyId = null, long? locationId = null, string? Roles = null, string? Permissions = null, Dictionary<string, string>? additionalFields = null)
        {
            var newUser = new AppUser();
            newUser.FirstName = firstName;
            newUser.LastName = lastName;
            newUser.Email = email;
            newUser.locale = "Eastern Standard Time";
            newUser.PhoneNumber = phoneNumber;
            newUser.UserName = email;
            newUser.NormalizedEmail = email.ToUpper();
            newUser.NormalizedUserName = email.ToUpper();
            newUser.EmailConfirmed = true;
            newUser.IsActive = true;
            newUser.Created = DateTimeOffset.Now;
            

            newUser.CompanyId = companyId;
            newUser.LocationId = locationId;

            IdentityResult identityResult = null;
            if (!String.IsNullOrWhiteSpace(password))
            {
                identityResult = await _userManager.CreateAsync(newUser, password);
            }
            else
            {
                identityResult = await _userManager.CreateAsync(newUser);
            }

            if (identityResult.Succeeded)
            {
                if (additionalFields != null)
                {

                    var customFields = await databaseContext.CustomFields.AsNoTracking()
                        .Where(c => c.CustomFieldPlatformType == CustomFieldPlatformType.Users)
                        .ToDictionaryAsync(c => c.Name, c => c.Id);

                    var newCustomFieldValues = additionalFields.Select(af => new UserCustomField
                    {
                        UserId = newUser.Id,
                        CustomFieldId = customFields[af.Key],
                        Value = af.Value,
                    }).ToList();

                    databaseContext.UserCustomFields.AddRange(newCustomFieldValues);

                    // Custom Fields 
                    //foreach (var additionalField in additionalFields)
                    //{
                    //    await databaseContext.UserClaims.AddAsync(new IdentityUserClaim<long>()
                    //    {
                    //        ClaimType = additionalField.Key,
                    //        ClaimValue = additionalField.Value,
                    //        UserId = newUser.Id
                    //    });
                    //}

                    await databaseContext.SaveChangesAsync();

                }

                if (!String.IsNullOrWhiteSpace(Permissions))
                {
                    var newClaimValues = new List<string>();

                    var arrayOfPermissionNames = Permissions.Split(",");

                    var usrClaim = await databaseContext.UserClaims
                        .Where(p => p.UserId == newUser.Id && p.ClaimType == "permissions")
                        .FirstOrDefaultAsync();

                    var arrayOfIdsForPermission = new List<string>();
                    foreach (var permissionName in arrayOfPermissionNames)
                    {


                        var permission = await databaseContext.Permissions
                            .Where(p => p.Name.ToLower() == permissionName.ToLower())
                            .AsNoTracking()
                            .FirstOrDefaultAsync();

                        if (permission != null)
                        {
                            arrayOfIdsForPermission.Add(permission.Id.ToString());
                        }
                    }

                    if (usrClaim != null)
                    {
                        usrClaim.ClaimValue = String.Join(",", arrayOfIdsForPermission);
                    }
                    else
                    {
                        databaseContext.UserClaims.Add(new IdentityUserClaim<long>()
                        {
                            UserId = newUser.Id,
                            ClaimType = "permissions",
                            ClaimValue = String.Join(",", arrayOfIdsForPermission)
                        });
                    }

                    await databaseContext.SaveChangesAsync();
                }

                //databaseContext.UserRoles.RemoveRange(databaseContext.UserRoles.Where(u => u.UserId == newUser.Id));
                //await databaseContext.SaveChangesAsync();

                if (!String.IsNullOrWhiteSpace(Roles))
                {
                    var rolesArray = Roles.Split(",");
                    foreach (var role in rolesArray)
                    {
                        var roleItem = await databaseContext.Roles
                            .Where(r => r.Name.ToLower() == role.ToLower())
                            .FirstOrDefaultAsync();

                        if (roleItem != null)
                        {
                            await databaseContext.UserRoles.AddAsync(new IdentityUserRole<long>()
                            {
                                RoleId = roleItem.Id,
                                UserId = newUser.Id,
                            });
                        }
                    }
                    await databaseContext.SaveChangesAsync();
                }

            }



            return identityResult.Succeeded;
        }

        public async Task<List<Company>> GetCompanies(string? name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return await databaseContext.Companies
                    .Where(s => !s.IsDeactivated)
                    .Take(20)
                    .AsNoTracking()
                    .ToListAsync();
            }
            else
            {
                name = name.ToLower();

                var result = await databaseContext.Companies
                    .AsNoTracking()
                    .Where(c => !c.IsDeactivated && c.Title.ToLower().Contains(name))
                    .Take(20)
                    .ToListAsync();

                return result;
            }
        }

        public async Task<List<Location>> GetLocations(long companyId, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return await databaseContext.Locations.AsNoTracking().Where(l => l.CompanyId == companyId).ToListAsync();
            }
            else
            {
                return await databaseContext.Locations.AsNoTracking().Where(c => c.CompanyId == companyId && c.Title.ToLower().Contains(name.ToLower())).ToListAsync();
            }
        }

        public async Task<string> ChangeUserPassword(long userId, string newPassword)
        {
            var message = "";

            var currentUser = await userManagementService.GetSignedInUser();
            if (currentUser.Roles.Any(a => a.Name.ToLower() == "admin"))
            {
                var usr = await databaseContext.Users.Where(u => u.Id == userId).FirstOrDefaultAsync();
                if (usr != null)
                {
                    if (String.IsNullOrWhiteSpace(usr.SecurityStamp))
                    {
                        usr.SecurityStamp = Guid.NewGuid().ToString("D");
                        await databaseContext.SaveChangesAsync();
                    }

                    var token = await _userManager.GeneratePasswordResetTokenAsync(usr);
                    var response = await _userManager.ResetPasswordAsync(usr, token, newPassword);

                    foreach (var error in response.Errors)
                    {
                        message = message + error.Description + "<br/>";
                    }
                }
            }
            else
            {
                throw new BadRequestException("Only \"Admin\" accounts can change passwords");
            }

            if (!String.IsNullOrWhiteSpace(message))
            {
                throw new BadRequestException(message);
            }

            return message;
        }


        public async Task CreateUserAccount(string FirstName, string LastName, string Email)
        {
            await databaseContext.Users.AddAsync(new AppUser()
            {
                FirstName = FirstName,
                LastName = LastName,
                UserName = Email,
                NormalizedUserName = Email,
                Email = Email,
                NormalizedEmail = Email,
                Created = SystemTime.Now,
                LastLoggedIn = SystemTime.Now,
                IsActive = false,
            });
            await databaseContext.SaveChangesAsync();
        }

        public async Task<List<CustomField>> GetAllCustomFields(CustomFieldPlatformType platformType)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            return await databaseContext.CustomFields
                .Include(c => c.CustomFieldTab)
                .Where(c => (c.CompanyId == null || c.CompanyId == signedInUser.CompanyId) && c.CustomFieldPlatformType == platformType)
                .Select(s => new CustomField()
                {
                    Id = s.Id,
                    CustomFieldPlatformType = s.CustomFieldPlatformType,
                    FieldType = s.FieldType,
                    GridSize = s.GridSize,
                    IsRequired = s.IsRequired,
                    Name = s.Name,
                    CustomFieldTab = s.CustomFieldTab
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<CustomFieldTab>> GetCustomTabs(CustomFieldPlatformType platformType)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            var fields = await databaseContext.CustomFieldsTab
                .Where(c => c.PlatformType == platformType && (c.CompanyId == null || c.CompanyId == signedInUser.CompanyId))
                .ToListAsync();

            return fields;
        }

        public async Task<Guid> CreateTab(string name, CustomFieldPlatformType platformType)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            var newTab = new CustomFieldTab()
            {
                Name = name,
                CompanyId = signedInUser.CompanyId,
                PlatformType = platformType
            };

            await databaseContext.CustomFieldsTab.AddAsync(newTab);
            await databaseContext.SaveChangesAsync();

            return newTab.Id;
        } 

        public async Task<CustomField?> GetCustomField(Guid id)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            return
                await databaseContext.CustomFields
                .AsNoTracking()
                .Where(a => a.Id == id && (a.CompanyId == null || a.CompanyId == signedInUser.CompanyId))
                .FirstOrDefaultAsync();
        }

        public async Task DeleteCustomField(Guid id)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            var customField = await databaseContext.CustomFields
                .Where(a => a.Id == id).FirstOrDefaultAsync();

            if (customField != null)
            {
                if (customField.CustomFieldPlatformType == CustomFieldPlatformType.Users)
                {
                    var userCustomFieldValues = await databaseContext.UserCustomFields.Where(f => f.CustomFieldId == customField.Id).ToListAsync();
                    
                    if (userCustomFieldValues != null)
                    {
                        databaseContext.UserCustomFields.RemoveRange(userCustomFieldValues);
                    }
                }
                else if (customField.CustomFieldPlatformType == CustomFieldPlatformType.Companies)
                {
                    var companyCustomFieldValues = await databaseContext.CompanyCustomFields.Where(f => f.CustomFieldId == customField.Id).ToListAsync();
                    if (companyCustomFieldValues != null)
                    {
                        databaseContext.CompanyCustomFields.RemoveRange(companyCustomFieldValues);
                    }
                }
                else if (customField.CustomFieldPlatformType == CustomFieldPlatformType.Locations)
                {

                }

                databaseContext.CustomFields.Remove(customField);
                await databaseContext.SaveChangesAsync();
            }
        }

        public async Task DeleteCustomTab(Guid id)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            var customTab = await databaseContext.CustomFieldsTab
                .Where(a => a.Id == id).FirstOrDefaultAsync();

            if (customTab != null)
            {
                var customFieldValues = await databaseContext.CustomFields.Where(f => f.TabId == id).ToListAsync();
                if (customFieldValues != null)
                {
                    for (int i = 0; i < customFieldValues.Count; i++) 
                        customFieldValues[i].TabId = null;
                }
               
                databaseContext.CustomFieldsTab.Remove(customTab);
                await databaseContext.SaveChangesAsync();
            }
        }

        public async Task UpdateCompany(CompanyEditParam param)
        {
            var company = await databaseContext.Companies
                .Where(c => c.Id == param.Id)
                .FirstOrDefaultAsync();

            if (company != null)
            {
                company.Title = param.Title;
                company.IsDeactivated = param.IsDeactivated;

                // new custom field logic here....
                foreach (var customField in param.CustomFields)
                {
                    var userCustomField = await databaseContext.CompanyCustomFields
                        .Where(c => c.CustomFieldId == customField.CustomFieldId && c.CompanyId == param.Id)
                        .FirstOrDefaultAsync();

                    if (userCustomField != null)
                    {
                        userCustomField.Value = customField.Value;
                    }
                    else
                    {
                        await databaseContext.CompanyCustomFields.AddAsync(new CompanyCustomField()
                        {
                            CustomFieldId = customField.CustomFieldId,
                            CompanyId = param.Id,
                            Value = customField.Value
                        });
                    }
                }

                await databaseContext.SaveChangesAsync();


            }
        }
    }
}
