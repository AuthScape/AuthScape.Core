using AuthScape.Models.Settings;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Services.IdentityServer
{
    public class SettingsService : ISettingsService
    {
        private readonly DatabaseContext dbContext;

        public SettingsService(DatabaseContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<List<Settings>> GetAllSettingsAsync()
        {
            return await dbContext.Settings
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Settings> GetSettingAsync(string name)
        {
            return await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Name == name);
        }

        public async Task<string> GetSettingValueAsync(string name)
        {
            var setting = await GetSettingAsync(name);
            return setting?.Value ?? string.Empty;
        }

        public async Task<bool> GetSettingValueAsBoolAsync(string name)
        {
            var value = await GetSettingValueAsync(name);
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task UpdateSettingAsync(string name, string value)
        {
            var setting = await GetSettingAsync(name);
            if (setting != null)
            {
                setting.Value = value;
                await dbContext.SaveChangesAsync();
            }
            else
            {
                throw new Exception($"Setting '{name}' not found");
            }
        }

        public async Task CreateSettingAsync(string name, string value, int settingTypeId = 0)
        {
            var existing = await GetSettingAsync(name);
            if (existing != null)
            {
                throw new Exception($"Setting '{name}' already exists");
            }

            var setting = new Settings
            {
                Id = Guid.NewGuid(),
                Name = name,
                Value = value,
                SettingTypeId = settingTypeId
            };

            dbContext.Settings.Add(setting);
            await dbContext.SaveChangesAsync();
        }

        public async Task DeleteSettingAsync(string name)
        {
            var setting = await GetSettingAsync(name);
            if (setting != null)
            {
                dbContext.Settings.Remove(setting);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
