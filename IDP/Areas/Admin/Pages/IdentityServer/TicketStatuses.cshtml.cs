using AuthScape.TicketSystem.Services;
using AuthScape.TicketSystem.Modals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class TicketStatusesModel : PageModel
    {
        private readonly ITicketService ticketService;

        public TicketStatusesModel(ITicketService ticketService)
        {
            this.ticketService = ticketService;
        }

        public List<TicketStatus> TicketStatuses { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            TicketStatuses = await ticketService.GetTicketStatuses();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Ticket status name is required";
                return RedirectToPage();
            }

            await ticketService.CreateTicketStatus(name);
            SuccessMessage = $"Ticket status '{name}' created successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string name, bool completedStep, bool archiveStep)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Ticket status name is required";
                return RedirectToPage();
            }

            await ticketService.UpdateTicketStatusConfig(id, name, completedStep, archiveStep);
            SuccessMessage = $"Ticket status '{name}' updated successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await ticketService.DeleteTicketStatus(id);
            SuccessMessage = "Ticket status deleted successfully";
            return RedirectToPage();
        }
    }
}
