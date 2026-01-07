using AuthScape.Models.Invite;
using AuthScape.Models.Users;
using CoreBackpack.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models.Invite;
using Services.Context;
using System.Text.Json;

namespace Services
{
    public interface IInviteService
    {
        Task<List<AppUser>> OnInviteUser(DatabaseContext _applicationDbContext, List<InviteRequest> userRequests, long inviterId);
        Task OnInviteCompleted(AppUser appUser, InviteViewModel inviteViewModel);
        Task OnInvitePageLoading(InviteViewModel inviteViewModel, AppUser dbUser);
        Task ApplyInviteRolesAndPermissions(long userId, Guid inviteId);
    }

    public class InviteService : IInviteService
    {
        readonly DatabaseContext databaseContext;
        public InviteService(DatabaseContext databaseContext)
        {
            this.databaseContext = databaseContext;
        }

        public async Task<List<AppUser>> OnInviteUser(DatabaseContext _applicationDbContext, List<InviteRequest> userRequests, long inviterId)
        {
            var newUserInvites = new List<AppUser>();
            foreach (var user in userRequests)
            {
                var newUser = new AppUser()
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.Email,
                    NormalizedUserName = user.Email.ToUpper(),
                    Email = user.Email,
                    NormalizedEmail = user.Email.ToUpper(),
                    EmailConfirmed = false,
                    Created = SystemTime.Now,
                    locale = user.Locale,
                    IsActive = false,
                    CompanyId = user.CompanyId,
                    LocationId = user.LocationId,
                    PasswordHash = null,
                    SecurityStamp = Guid.NewGuid().ToString("D"),
                    ConcurrencyStamp = null,
                    PhoneNumberConfirmed = false,
                    TwoFactorEnabled = false,
                    LockoutEnabled = false,
                    PhoneNumber = user.PhoneNumber,
                    PhotoUri = null,
                    AccessFailedCount = 0
                };

                _applicationDbContext.Users.Add(newUser);
                await _applicationDbContext.SaveChangesAsync();

                // Create UserInvite record to track roles/permissions
                var userInvite = new UserInvite
                {
                    InvitedUserId = newUser.Id,
                    InviterId = inviterId,
                    CompanyId = user.CompanyId,
                    LocationId = user.LocationId,
                    AssignedRoles = user.RoleIds != null && user.RoleIds.Any()
                        ? JsonSerializer.Serialize(user.RoleIds)
                        : null,
                    AssignedPermissions = user.PermissionIds != null && user.PermissionIds.Any()
                        ? JsonSerializer.Serialize(user.PermissionIds)
                        : null,
                    Status = InviteStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                    Created = DateTimeOffset.UtcNow
                };

                _applicationDbContext.UserInvites.Add(userInvite);
                await _applicationDbContext.SaveChangesAsync();

                // add new user to list to send an invite
                newUserInvites.Add(newUser);
            }

            return newUserInvites;
        }

        public async Task ApplyInviteRolesAndPermissions(long userId, Guid inviteId)
        {
            var invite = await databaseContext.UserInvites
                .Where(i => i.Id == inviteId && i.InvitedUserId == userId)
                .FirstOrDefaultAsync();

            if (invite == null) return;

            // Apply roles
            if (!string.IsNullOrWhiteSpace(invite.AssignedRoles))
            {
                var roleIds = JsonSerializer.Deserialize<List<long>>(invite.AssignedRoles);
                if (roleIds != null)
                {
                    foreach (var roleId in roleIds)
                    {
                        var existingRole = await databaseContext.UserRoles
                            .Where(ur => ur.UserId == userId && ur.RoleId == roleId)
                            .FirstOrDefaultAsync();

                        if (existingRole == null)
                        {
                            await databaseContext.UserRoles.AddAsync(new IdentityUserRole<long>
                            {
                                UserId = userId,
                                RoleId = roleId
                            });
                        }
                    }
                }
            }

            // Apply permissions
            if (!string.IsNullOrWhiteSpace(invite.AssignedPermissions))
            {
                var permissionIds = JsonSerializer.Deserialize<List<Guid>>(invite.AssignedPermissions);
                if (permissionIds != null && permissionIds.Any())
                {
                    var permissionValue = string.Join(",", permissionIds);

                    var existingClaim = await databaseContext.UserClaims
                        .Where(c => c.UserId == userId && c.ClaimType == "permissions")
                        .FirstOrDefaultAsync();

                    if (existingClaim != null)
                    {
                        existingClaim.ClaimValue = permissionValue;
                    }
                    else
                    {
                        await databaseContext.UserClaims.AddAsync(new IdentityUserClaim<long>
                        {
                            UserId = userId,
                            ClaimType = "permissions",
                            ClaimValue = permissionValue
                        });
                    }
                }
            }

            // Mark invite as completed
            invite.Status = InviteStatus.Accepted;
            invite.CompletedAt = DateTimeOffset.UtcNow;

            await databaseContext.SaveChangesAsync();
        }

        public async Task OnInviteCompleted(AppUser appUser, InviteViewModel inviteViewModel)
        {
            appUser.FirstName = inviteViewModel.FirstName;
            appUser.LastName = inviteViewModel.LastName;
            appUser.IsActive = true;
            appUser.WhenInviteSent = null;
            appUser.EmailConfirmed = true;

            if (appUser.CompanyId != null)
            {
                var company = await databaseContext.Companies.Where(c => c.Id == appUser.CompanyId).FirstOrDefaultAsync();
                if (company != null)
                {
                    company.Title = inviteViewModel.CompanyName;
                }
            }
        }

        public async Task OnInvitePageLoading(InviteViewModel inviteViewModel, AppUser dbUser)
        {

        }
    }
}
