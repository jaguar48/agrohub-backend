using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Helpers
{
    public class WalletIdGenerator
    {
        public static string GenerateWalletId()
        {
            string startWith = "62";
            var miliseconds = string.Format("{0:000}", DateTime.Now.Millisecond);
            var year = DateTime.Now.ToString("yy");
            var day = RandomNumberGenerator.GetInt32(100, 999).ToString();
            var accountNumber = startWith + miliseconds + year + day;

            return accountNumber;
        }
    }
}
