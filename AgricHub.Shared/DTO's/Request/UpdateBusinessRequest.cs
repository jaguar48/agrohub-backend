using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class UpdateBusinessRequest
    {
        public string BusinessName { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public IFormFile? File { get; set; }
    }
}
