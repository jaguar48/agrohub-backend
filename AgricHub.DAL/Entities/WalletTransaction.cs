using AgricHub.DAL.Entities.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{


    public class WalletTransaction
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }  // Make nullable
        public int? ConsultantId { get; set; }  // Add this

        public decimal Amount { get; set; }
        public string ? PaystackTransactionReference { get; set; }
        public string TransactionType { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; }

        [ForeignKey(nameof(ConsultantId))]
        public virtual Consultant Consultant { get; set; }  // This is enough for EF to understand the relationship
    }
}
