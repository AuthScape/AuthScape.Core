﻿namespace AuthScape.Models.Users
{
    public class UserSummary
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Created { get; set; }
    }
}