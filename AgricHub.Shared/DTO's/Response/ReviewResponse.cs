using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
  
    public class ReviewResponse
    {
        public int Id { get; set; }
        public string ConsultationId { get; set; }
        public int CustomerId { get; set; }
        public int ConsultantId { get; set; }
        public int? ServiceId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string CreatedAt { get; set; }
        public string CustomerName { get; set; }  // ← for display
        public string? ServiceName { get; set; }
        public string? ConsultantReply { get; set; }
        public string? ConsultantReplyAt { get; set; }

    }
}