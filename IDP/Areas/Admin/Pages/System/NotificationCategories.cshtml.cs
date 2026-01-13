using AuthScape.Models.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.System
{
    [Authorize]
    public class NotificationCategoriesModel : PageModel
    {
        private readonly DatabaseContext _context;

        public NotificationCategoriesModel(DatabaseContext context)
        {
            _context = context;
        }

        public List<NotificationCategoryConfig> Categories { get; set; } = new();
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Categories = await _context.NotificationCategoryConfigs
                .OrderBy(c => c.Id)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name, string description, bool isActive = true)
        {
            try
            {
                // Check if name already exists
                var existing = await _context.NotificationCategoryConfigs
                    .FirstOrDefaultAsync(c => c.Name == name);

                if (existing != null)
                {
                    ErrorMessage = "A category with this name already exists.";
                    await OnGetAsync();
                    return Page();
                }

                var category = new NotificationCategoryConfig
                {
                    Name = name,
                    Description = description,
                    IsActive = isActive,
                    Created = DateTimeOffset.UtcNow,
                    Modified = DateTimeOffset.UtcNow
                };

                _context.NotificationCategoryConfigs.Add(category);
                await _context.SaveChangesAsync();

                SuccessMessage = "Category created successfully!";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error creating category: {ex.Message}";
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string name, string description, bool isActive)
        {
            try
            {
                var category = await _context.NotificationCategoryConfigs.FindAsync(id);
                if (category == null)
                {
                    ErrorMessage = "Category not found.";
                    await OnGetAsync();
                    return Page();
                }

                // Check if name already exists for a different category
                var existing = await _context.NotificationCategoryConfigs
                    .FirstOrDefaultAsync(c => c.Name == name && c.Id != id);

                if (existing != null)
                {
                    ErrorMessage = "A category with this name already exists.";
                    await OnGetAsync();
                    return Page();
                }

                category.Name = name;
                category.Description = description;
                category.IsActive = isActive;
                category.Modified = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync();

                SuccessMessage = "Category updated successfully!";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating category: {ex.Message}";
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var category = await _context.NotificationCategoryConfigs.FindAsync(id);
                if (category == null)
                {
                    ErrorMessage = "Category not found.";
                    await OnGetAsync();
                    return Page();
                }

                // Soft delete
                category.IsActive = false;
                category.Modified = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync();

                SuccessMessage = "Category deleted successfully!";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting category: {ex.Message}";
            }

            await OnGetAsync();
            return Page();
        }
    }
}
