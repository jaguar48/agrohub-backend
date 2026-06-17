using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class CreateCategoryRequest
    {
        [Required(ErrorMessage = "name is required")]
        public string name { get; set; }
    }
}
