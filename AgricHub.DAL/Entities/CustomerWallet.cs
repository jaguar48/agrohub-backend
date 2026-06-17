using AgricHub.DAL.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    namespace AgricHub.DAL.Entities
    {
        public class CustomerWallet
        {
            public int Id { get; set; }
            public int CustomerId { get; set; }
            public Customer Customer { get; set; }
            public string WalletNo { get; set; } // Generated using WalletIdGenerator
            public decimal Balance { get; set; } // Available balance in NGN
            public bool IsActive { get; set; } = true;
            public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        }
    
}
