using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IChatServices
{// AgricHub.BLL/Interfaces/ChatServices/IDailyService.cs

        public interface IDailyService
        {
            /// <summary>
            /// Creates a Daily.co video room and returns its join URL.
            /// The API key never leaves the server.
            /// </summary>
            Task<string> CreateRoomAsync(string roomName, int expirySeconds = 7200);
       Task<string> CreateMeetingTokenAsync(string roomName, bool isOwner = false, string? userName = null, int expirySeconds = 7200);
        }
    }