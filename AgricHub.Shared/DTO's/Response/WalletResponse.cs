using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class WalletResponse
    {
        public string UserId { get; set; }           // ✅ GUID
        public string UserName { get; set; }
        public string UserType { get; set; }         // "Customer" or "Consultant"
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}