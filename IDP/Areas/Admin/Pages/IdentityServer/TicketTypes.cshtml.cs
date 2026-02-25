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
    public class TicketTypesModel : PageModel
    {
        private readonly ITicketService ticketService;

        public TicketTypesModel(ITicketService ticketService)
        {
            this.ticketService = ticketService;
        }

        public List<TicketType> TicketTypes { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            TicketTypes = await ticketService.GetTicketTypes();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Ticket type name is required";
                return RedirectToPage();
            }

            await ticketService.CreateTicketType(name);
            SuccessMessage = $"Ticket type '{name}' created successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Ticket type name is required";
                return RedirectToPage();
            }

            await ticketService.UpdateTicketTypeConfig(id, name);
            SuccessMessage = $"Ticket type '{name}' updated successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await ticketService.DeleteTicketType(id);
            SuccessMessage = "Ticket type deleted successfully";
            return RedirectToPage();
        }
    }
}
