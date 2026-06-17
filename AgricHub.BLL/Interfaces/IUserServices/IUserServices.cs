using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IUserServices
{
    public interface IUserServices
    {
        Task<ApplicationUser> RegisterUser(UserForRegistrationRequest Request);

    }
}
