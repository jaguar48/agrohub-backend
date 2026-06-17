using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class WalletTopUpResponse
    {
        public string AccessCode { get; set; }
        public string Reference { get; set; }
        public string PaymentUrl { get; set; }
        public string Message { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
    }
}
