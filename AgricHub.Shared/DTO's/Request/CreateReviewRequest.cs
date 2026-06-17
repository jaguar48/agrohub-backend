using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{


  
        public class CreateReviewRequest
        {
            public Guid ConsultationId { get; set; }
            public int ConsultantId { get; set; }  
            public int Rating { get; set; }         
            public string? Comment { get; set; }
        }
    

}
