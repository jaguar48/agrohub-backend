using AgricHub.DAL.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{
    public class ChatSession
    {
        public Guid Id { get; set; }
        public int CustomerId { get; set; }
        public int ConsultantId { get; set; }
        public int? ServiceId { get; set; } 
        public string SendbirdChannelUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public Customer Customer { get; set; }
        public Consultant Consultant { get; set; }
        public Service? Service { get; set; }
    }
}
