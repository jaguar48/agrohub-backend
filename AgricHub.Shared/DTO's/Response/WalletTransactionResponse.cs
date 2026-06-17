using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class WalletTransactionResponse
    {
        public int Id { get; set; }                      // ✅ Keep as int (it's fine)
        public decimal Amount { get; set; }
        public string TransactionType { get; set; }
        public string Status { get; set; }
        public string PaystackTransactionReference { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}