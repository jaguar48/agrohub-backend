using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class ConsultantRegistrationRequest
    {
        [Required(ErrorMessage = "Firstname is required")]
        public string FirstName { get; init; }

        [Required(ErrorMessage = "Lastname is required")]
        public string LastName { get; init; }

      /*  [Required(ErrorMessage = "BusinessName is required")]
        public string BusinessName { get; init; }*/

        [Required(ErrorMessage = "UserName is required")]
        public string UserName { get; init; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; init; }
        public string BusinessName { get; init; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; init; }
        public string CountryId { get; set; }
        public string StateId { get; set; }
        public string Address { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        public string PhoneNumber { get; init; }
    }
}
