using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IAgrichub_Services
{
    public interface IBusinessForService
    {

        Task<string> AddServiceAsync(CreateServiceRequest serviceRequest);
        Task<string> UpdateServiceAsync(int serviceId, CreateServiceRequest serviceRequest);

        Task<ViewServiceResponse> ViewServiceAsync(int serviceId);
        Task<IEnumerable<ViewServiceResponse>> ViewAllServicesAsync();
       Task<IEnumerable<ViewServiceResponse>> ViewOwnBusinessServicesAsync();
       Task<string> DeleteServiceAsync(int serviceId);
    }
}
