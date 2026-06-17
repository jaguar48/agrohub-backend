using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CountryId { get; set; }
        public string StateId { get; set; }
        public string Address { get; set; }
        public string? VerificationToken { get; set; }
    }
}
