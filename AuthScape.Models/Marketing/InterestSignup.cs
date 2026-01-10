using System;
using System.ComponentModel.DataAnnotations;

namespace AuthScape.Models.Marketing
{
    /// <summary>
    /// Represents a user interest signup for CommandDeck marketing notifications
    /// </summary>
    public class InterestSignup
    {
        public int Id { get; set; }

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

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
