using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class UpdateCustomerProfileRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string LastName { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        [StringLength(200)]
        public string Address { get; set; }

        public string? CountryId { get; set; }
        public string? StateId { get; set; }
    }
}
