using AuthScape.TicketSystem.Modals;
using Models.Email;

namespace Services
{
    // DEPRECATED: Use AuthScape.API.Services.INotificationService instead for the full notification system
    // This interface is kept for backward compatibility with ticket/invoice notifications
    public interface ILegacyNotificationService
    {
        Task NotifyTicketCreated(Ticket ticket);
        Task NotifyTicketMessageCreated(long ticketId, string fromEmail, string message, string firstName, string lastName);
        Task SendInvoice(long companyId, long LocationId, InvoiceEmail invoiceEmail);
    }

    public class LegacyNotificationService : ILegacyNotificationService
    {
        public async Task NotifyTicketCreated(Ticket ticket)
        {
            // Notify your team that a ticket was created via email or teams]
        }

        public async Task NotifyTicketMessageCreated(long ticketId, string fromEmail, string message, string firstName, string lastName)
        {
            // Notify your team that a ticket message was created via email or teams
        }

        public async Task SendInvoice(long companyId, long LocationId, InvoiceEmail invoiceEmail)
        {
            //await sendGridService.Send(users, "", new InvoiceEmail()
            //{
            //    amountdue = invoice.BalanceDue.ToString("C"),
            //    paylink = paymentLink
            //});
        }
    }
}
