using AuthScape.Document.Mapping.Models;
using AuthScape.TicketSystem.Modals;
using AuthScape.TicketSystem.Models;
using AuthScape.TicketSystem.Services;
using CoreBackpack.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.TicketSystem.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TicketController : ControllerBase
    {
        readonly ITicketService ticketService;

        public TicketController(ITicketService ticketService)
        {
            this.ticketService = ticketService;
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CreateTicket(CreateTicketParam param)
        {
            var ticketId = await ticketService.CreateTicket(param.TicketTypeId, param.TicketStatusId, param.Description, param.Message, param.PriorityLevel);
            return Ok(ticketId);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTicketPublic([FromForm] CreatePublicTicketParam param)
        {
            var ticketId = await ticketService.CreateTicketPublic(param.Email, param.FirstName, param.LastName, param.TicketTypeId, param.TicketStatusId, param.Description, param.Message, param.CompanyName, param.JobTitle, param.Address, param.PhoneNumber, param.File,  param.PrivateLabelCompanyId);
            return Ok(ticketId);
        }

        [HttpPost]
        public async Task<IActionResult> CreateMessage(CreateTicketMessageParam param)
        {
            await ticketService.CreateTicketMessage(param.TicketId, param.Name, param.Message, param.CreatedByUserId, param.IsNote);
            return Ok();
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetMessages(long ticketId, bool isNote, int pageNumber = 1, int pageSize = 20)
        {
            return Ok(await ticketService.GetTicketMessages(ticketId, isNote, pageNumber, pageSize));
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetTickets(GetTicketRequestParam param)
        {
            var tickets = await ticketService.GetTickets(param.offset, param.length, param.ticketStatusId, param.ticketTypeId, param.PrivateLabelCompanyId);
            return Ok(new ReactDataTable
            {
                draw = 0,
                recordsTotal = tickets.total,
                recordsFiltered = tickets.total,
                data = tickets.ToList()
            });
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetTicket(long ticketId)
        {
            return Ok(await ticketService.GetTicket(ticketId));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> FindUser(string query)
        {
            return Ok(await ticketService.FindUser(query));
        }

        [HttpDelete]
        public async Task<IActionResult> ArchiveTicket(long id)
        {
            await ticketService.ArchiveTicket(id);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetStatuses()
        {
            return Ok(await ticketService.GetTicketStatuses());
        }

        [HttpGet]
        public async Task<IActionResult> GetTicketTypes()
        {
            return Ok(await ticketService.GetTicketTypes());
        }

        [HttpPut]
        public async Task<IActionResult> UpdateParticipants(AddParticipantsViewModel addParticipantsViewModel)
        {
            await ticketService.UpdateParticipants(addParticipantsViewModel.TicketId, addParticipantsViewModel.Participants);
            return Ok();
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateStatus(UpdateTicketStatus ticketStatus)
        {
            await ticketService.UpdateStatus(ticketStatus.Id, ticketStatus.TicketStatusId);
            return Ok();
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateTicketType(UpdateTicketType ticketType)
        {
            await ticketService.UpdateTicketType(ticketType.Id, ticketType.TicketTypeId);
            return Ok();
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateTicketPriority(UpdateTicketPriority ticketPriority)
        {
            await ticketService.UpdateTicketPriority(ticketPriority.Id, ticketPriority.PriorityLevel);
            return Ok();
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateDescription(UpdateTicketDescription ticketDescription)
        {
            await ticketService.UpdateDescription(ticketDescription.Id, ticketDescription.Description);
            return Ok();
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateCompany(UpdateTicketCompany ticketCompany)
        {
            await ticketService.UpdateCompany(ticketCompany.Id, ticketCompany.CompanyId, ticketCompany.CompanyName);
            return Ok();
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateLocation(UpdateTicketLocation ticketLocation)
        {
            await ticketService.UpdateLocation(ticketLocation.Id, ticketLocation.LocationId, ticketLocation.LocationName);
            return Ok();
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AddAttachment([FromForm] AddAttachmentParam param)
        {
            var attachment = await ticketService.AddAttachment(param.TicketId, param.File);
            return Ok(attachment);
        }

        [HttpDelete]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteAttachment(long attachmentId)
        {
            await ticketService.DeleteAttachment(attachmentId);
            return Ok();
        }
    }

    public class UpdateTicketPriority
    {
        public long Id { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
    }

    public class UpdateTicketStatus
    {
        public long Id { get; set; }
        public int TicketStatusId { get; set; }
    }

    public class UpdateTicketType
    {
        public long Id { get; set; }
        public int TicketTypeId { get; set; }
    }

    public class UpdateTicketDescription
    {
        public long Id { get; set; }
        public string Description { get; set; }
    }

    public class UpdateTicketCompany
    {
        public long Id { get; set; }
        public long? CompanyId { get; set; }
        public string? CompanyName { get; set; }
    }

    public class UpdateTicketLocation
    {
        public long Id { get; set; }
        public long? LocationId { get; set; }
        public string? LocationName { get; set; }
    }

    public class CreateTicketParam
    {
        public int TicketTypeId { get; set; }
        public int TicketStatusId { get; set; }
        public string? Description { get; set; }
        public string Message { get; set; }
        public int PriorityLevel { get; set; } = 2;
    }

    public class CreatePublicTicketParam
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? CompanyName { get; set; }
        public string? JobTitle { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string Email { get; set; }
        public int TicketTypeId { get; set; }
        public int TicketStatusId { get; set; }
        public string? Message { get; set; }
        public string? Description { get; set; }
        public long? PrivateLabelCompanyId { get; set; }
        public IFormFile? File { get; set; }

    }

    public class CreateTicketMessageParam
    {
        public long TicketId { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public long? CreatedByUserId { get; set; } = null;
        public bool IsNote { get; set; }
    }

    public class GetTicketRequestParam
    {
        public long? PrivateLabelCompanyId { get; set; } = null;
        public int offset { get; set; } = 0;
        public int length { get; set; } = 20;
        public int? ticketStatusId { get; set; } = null;
        public int? ticketTypeId { get; set; } = null;
    }

    public class AddAttachmentParam
    {
        public long TicketId { get; set; }
        public IFormFile File { get; set; }
    }
}