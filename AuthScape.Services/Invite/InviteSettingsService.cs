using AuthScape.Models.Invite;
using Microsoft.EntityFrameworkCore;
using Models.Invite;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthScape.Services.Invite
{
    public interface IInviteSettingsService
    {
        Task<InviteSettingsDto> GetGlobalSettingsAsync();
        Task<InviteSettingsDto?> GetCompanySettingsAsync(long companyId);
        Task<InviteSettingsDto> GetEffectiveSettingsAsync(long? companyId);
        Task<List<InviteSettingsDto>> GetAllCompanyOverridesAsync();
        Task SaveGlobalSettingsAsync(UpdateInviteSettingsDto dto);
        Task SaveCompanySettingsAsync(UpdateInviteSettingsDto dto);
        Task DeleteCompanyOverrideAsync(long companyId);
        Task<InviteValidationResult> ValidateInviteRequestAsync(
            InviteRequest request,
            long inviterId,
            long? inviterCompanyId);
        Task<List<long>> GetInviterRoleIdsAsync(long inviterId);
        Task<List<Guid>> GetInviterPermissionIdsAsync(long inviterId);
    }

    public class InviteSettingsService : IInviteSettingsService
    {
        private readonly DatabaseContext _context;

        public InviteSettingsService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<InviteSettingsDto> GetGlobalSettingsAsync()
        {
            var settings = await _context.InviteSettings
                .Where(s => s.CompanyId == null)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                // Return defaults if no global settings exist
                return new InviteSettingsDto
                {
                    EnableInviteToCompany = true,
                    EnableInviteToLocation = true,
                    AllowSettingPermissions = true,
                    AllowSettingRoles = true,
                    EnforceSamePermissions = false,
                    EnforceSameRole = false
                };
            }

            return MapToDto(settings);
        }

        public async Task<InviteSettingsDto?> GetCompanySettingsAsync(long companyId)
        {
            var settings = await _context.InviteSettings
                .Where(s => s.CompanyId == companyId)
                .FirstOrDefaultAsync();

            if (settings == null)
                return null;

            var companyName = await _context.Companies
                .Where(c => c.Id == companyId)
                .Select(c => c.Title)
                .FirstOrDefaultAsync();

            return MapToDto(settings, companyName);
        }

        public async Task<InviteSettingsDto> GetEffectiveSettingsAsync(long? companyId)
        {
            if (companyId.HasValue)
            {
                var companySettings = await GetCompanySettingsAsync(companyId.Value);
                if (companySettings != null)
                    return companySettings;
            }
            return await GetGlobalSettingsAsync();
        }

        public async Task<List<InviteSettingsDto>> GetAllCompanyOverridesAsync()
        {
            var overrides = await _context.InviteSettings
                .Where(s => s.CompanyId != null)
                .ToListAsync();

            // Get company names for all overrides
            var companyIds = overrides.Select(s => s.CompanyId!.Value).ToList();
            var companyNames = await _context.Companies
                .Where(c => companyIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Title);

            return overrides
                .Select(s => MapToDto(s, s.CompanyId.HasValue && companyNames.ContainsKey(s.CompanyId.Value)
                    ? companyNames[s.CompanyId.Value]
                    : null))
                .OrderBy(s => s.CompanyName)
                .ToList();
        }

        public async Task SaveGlobalSettingsAsync(UpdateInviteSettingsDto dto)
        {
            var existing = await _context.InviteSettings
                .Where(s => s.CompanyId == null)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                existing = new InviteSettings
                {
                    CompanyId = null,
                    Created = DateTimeOffset.UtcNow
                };
                _context.InviteSettings.Add(existing);
            }

            existing.EnableInviteToCompany = dto.EnableInviteToCompany;
            existing.EnableInviteToLocation = dto.EnableInviteToLocation;
            existing.AllowSettingPermissions = dto.AllowSettingPermissions;
            existing.AllowSettingRoles = dto.AllowSettingRoles;
            existing.EnforceSamePermissions = dto.EnforceSamePermissions;
            existing.EnforceSameRole = dto.EnforceSameRole;
            existing.LastModified = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task SaveCompanySettingsAsync(UpdateInviteSettingsDto dto)
        {
            if (!dto.CompanyId.HasValue)
                throw new ArgumentException("CompanyId is required for company settings");

            var existing = await _context.InviteSettings
                .Where(s => s.CompanyId == dto.CompanyId)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                existing = new InviteSettings
                {
                    CompanyId = dto.CompanyId,
                    Created = DateTimeOffset.UtcNow
                };
                _context.InviteSettings.Add(existing);
            }

            existing.EnableInviteToCompany = dto.EnableInviteToCompany;
            existing.EnableInviteToLocation = dto.EnableInviteToLocation;
            existing.AllowSettingPermissions = dto.AllowSettingPermissions;
            existing.AllowSettingRoles = dto.AllowSettingRoles;
            existing.EnforceSamePermissions = dto.EnforceSamePermissions;
            existing.EnforceSameRole = dto.EnforceSameRole;
            existing.LastModified = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteCompanyOverrideAsync(long companyId)
        {
            var settings = await _context.InviteSettings
                .Where(s => s.CompanyId == companyId)
                .FirstOrDefaultAsync();

            if (settings != null)
            {
                _context.InviteSettings.Remove(settings);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<InviteValidationResult> ValidateInviteRequestAsync(
            InviteRequest request,
            long inviterId,
            long? inviterCompanyId)
        {
            var result = new InviteValidationResult { IsValid = true };
            var settings = await GetEffectiveSettingsAsync(inviterCompanyId);
            result.EffectiveSettings = settings;

            // Validate company invite settings
            if (request.CompanyId.HasValue && !settings.EnableInviteToCompany)
            {
                result.IsValid = false;
                result.Errors.Add("Inviting users to a company is disabled");
            }

            // Validate location invite settings
            if (request.LocationId.HasValue && !settings.EnableInviteToLocation)
            {
                result.IsValid = false;
                result.Errors.Add("Inviting users to a location is disabled");
            }

            // Validate role assignments
            if (request.RoleIds?.Any() == true)
            {
                if (!settings.AllowSettingRoles)
                {
                    result.IsValid = false;
                    result.Errors.Add("Setting roles for invited users is disabled");
                }
                else if (settings.EnforceSameRole)
                {
                    var inviterRoles = await GetInviterRoleIdsAsync(inviterId);
                    var invalidRoles = request.RoleIds.Except(inviterRoles).ToList();
                    if (invalidRoles.Any())
                    {
                        result.IsValid = false;
                        result.Errors.Add("You can only assign roles that you have");
                    }
                }
            }

            // Validate permission assignments
            if (request.PermissionIds?.Any() == true)
            {
                if (!settings.AllowSettingPermissions)
                {
                    result.IsValid = false;
                    result.Errors.Add("Setting permissions for invited users is disabled");
                }
                else if (settings.EnforceSamePermissions)
                {
                    var inviterPermissions = await GetInviterPermissionIdsAsync(inviterId);
                    var invalidPermissions = request.PermissionIds.Except(inviterPermissions).ToList();
                    if (invalidPermissions.Any())
                    {
                        result.IsValid = false;
                        result.Errors.Add("You can only assign permissions that you have");
                    }
                }
            }

            return result;
        }

        public async Task<List<long>> GetInviterRoleIdsAsync(long inviterId)
        {
            return await _context.UserRoles
                .Where(ur => ur.UserId == inviterId)
                .Select(ur => ur.RoleId)
                .ToListAsync();
        }

        public async Task<List<Guid>> GetInviterPermissionIdsAsync(long inviterId)
        {
            // Get permissions from user claims
            var permissionClaim = await _context.UserClaims
                .Where(c => c.UserId == inviterId && c.ClaimType == "permissions")
                .Select(c => c.ClaimValue)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(permissionClaim))
                return new List<Guid>();

            // Parse comma-separated GUIDs
            return permissionClaim
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var guid) ? guid : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();
        }

        private static InviteSettingsDto MapToDto(InviteSettings settings, string? companyName = null)
        {
            return new InviteSettingsDto
            {
                Id = settings.Id,
                CompanyId = settings.CompanyId,
                CompanyName = companyName,
                EnableInviteToCompany = settings.EnableInviteToCompany,
                EnableInviteToLocation = settings.EnableInviteToLocation,
                AllowSettingPermissions = settings.AllowSettingPermissions,
                AllowSettingRoles = settings.AllowSettingRoles,
                EnforceSamePermissions = settings.EnforceSamePermissions,
                EnforceSameRole = settings.EnforceSameRole
            };
        }
    }
}
