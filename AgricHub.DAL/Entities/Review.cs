using AgricHub.DAL.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{
    public class Review
    {
        public int Id { get; set; }

        public Guid ConsultationId { get; set; }
        public Consultation Consultation { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public int ConsultantId { get; set; }
        public Consultant Consultant { get; set; }

        public int? ServiceId { get; set; }
        public Service Service { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ConsultantReply { get; set; }
        public DateTime? ConsultantReplyAt { get; set; }
    }


}
