﻿
using AuthScape.UserManageSystem.Models;
using Models.Users;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.Users
{
    public class Location
    {
        public long Id { get; set; }
        public string Title { get; set; }


        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }

        public float? lat { get; set; }
        public float? lng { get; set; }


        public bool IsDeactivated { get; set; }


        public long? CompanyId { get; set; }
        public Company? Company { get; set; }
        public ICollection<AppUser> Users { get; set; }
        public ICollection<UserLocations> UserLocations { get; set; }


        [NotMapped]
        public List<CustomFieldResult> CustomFields { get; set; }

        [NotMapped]
        public bool IsActive { get; set; }
    }
}