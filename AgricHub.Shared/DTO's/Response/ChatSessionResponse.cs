using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{


    public class ChatSessionResponse
    {
        public Guid Id { get; set; }               // ChatSession PK
        public int CustomerId { get; set; }        // DB PK
        public string CustomerUserId { get; set; } // ASP.NET Identity UserId
        public int ConsultantId { get; set; }      // DB PK
        public string ConsultantUserId { get; set; }
        public int? ServiceId { get; set; }
        public string SendbirdChannelUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerName { get; set; }
        public string ConsultantName { get; set; }
        public string ServiceName { get; set; }
    }


    public class ChatInitiateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ChannelUrl { get; set; }
        public string ChatSessionId { get; set; }  // ← ADD THIS PROPERTY!
    }
}


