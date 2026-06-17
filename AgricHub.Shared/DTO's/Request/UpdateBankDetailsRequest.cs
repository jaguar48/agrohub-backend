using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class UpdateBankDetailsRequest
    {
        [Required]
        public string BankName { get; set; }

        [Required]
        public string BankCode { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 10)]
        public string AccountNumber { get; set; }
    }
}
