using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class InitiateChatRequest
    {
        public string ConsultantUserId { get; set; }
        public int? ServiceId { get; set; }
    }
}
