using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class BankInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class BankAccountDetails
    {
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public string BankCode { get; set; }
    }
}
