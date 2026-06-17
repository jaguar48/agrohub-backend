using AgricHub.DAL.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{
    public class PendingTransaction
    {
        public Guid Id { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public Guid ConsultationId { get; set; }
        public Consultation Consultation { get; set; }
        public decimal Amount { get; set; } // Amount held in escrow
        public string Status { get; set; } // Pending, Released, Refunded, PartiallyRefunded
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }
}
