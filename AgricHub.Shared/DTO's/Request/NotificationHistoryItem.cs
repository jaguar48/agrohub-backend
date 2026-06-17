using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class NotificationHistoryItem
    {
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info";
        public long CreatedAt { get; set; }
    }
}