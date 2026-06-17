using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.UserProfile
{
    public class UserProfileResponse
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string CountryId { get; set; }
        public string StateId { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string UserType { get; set; } // "Customer" or "Consultant"
        
        // Consultant Specific
        public string? BusinessName { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string CountryId { get; set; }
        public string StateId { get; set; }
        
        // Consultant Specific
        public string? BusinessName { get; set; }
    }
}
