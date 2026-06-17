using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class CreateBusinessRequest
    {
       
        [Required(ErrorMessage = "name is required")]
        public string BusinessName { get; set; }
        [Required(ErrorMessage = "name is required")]
        public string Description { get; set; }

        public string Address { get; set; }
     

        public IFormFile? File { get; set; }
        public DateTime DateCreated { get; set; } = new DateTime();
    }
}


