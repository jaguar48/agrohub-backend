using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IUserServices
{

    public interface IConsultantVerificationService
{
    Task<object> GetVerificationStatusAsync();
    Task SubmitVerificationAsync(SubmitVerificationRequest req);
}

public class SubmitVerificationRequest
{
    public IFormFile BusinessReg { get; set; }
    public IFormFile Credentials { get; set; }
    public IFormFile? GovernmentId { get; set; }
}
}

