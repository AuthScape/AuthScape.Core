using AuthScape.Models.Marketing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InterestController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public InterestController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitInterest([FromBody] InterestSignupDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Check if email already exists
                var existingSignup = await _context.InterestSignups
                    .FirstOrDefaultAsync(x => x.Email.ToLower() == dto.Email.ToLower());

                if (existingSignup != null)
                {
                    // Update existing record
                    existingSignup.FirstName = dto.FirstName;
                    existingSignup.LastName = dto.LastName;
                    existingSignup.MostExcitedAbout = dto.MostExcitedAbout;
                    existingSignup.FeatureRequests = dto.FeatureRequests;
                    existingSignup.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new signup
                    var signup = new InterestSignup
                    {
                        Email = dto.Email,
                        FirstName = dto.FirstName,
                        LastName = dto.LastName,
                        MostExcitedAbout = dto.MostExcitedAbout,
                        FeatureRequests = dto.FeatureRequests,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.InterestSignups.Add(signup);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Thank you for your interest!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving interest signup: {ex.Message}");
                return StatusCode(500, new { success = false, message = "An error occurred. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSignups()
        {
            var signups = await _context.InterestSignups
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(signups);
        }
    }

    public class InterestSignupDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string MostExcitedAbout { get; set; } = string.Empty;

        public string? FeatureRequests { get; set; }
    }
}
