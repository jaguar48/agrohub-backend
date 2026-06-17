using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using AutoMapper;

namespace AgricHub.BLL.MappingProfiles
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // ✅ Service
            CreateMap<CreateServiceRequest, Service>();
            CreateMap<Service, ViewServiceResponse>()
                .ForMember(dest => dest.BusinessName, opt => opt.MapFrom(src => src.Business.BusinessName));

            CreateMap<ServicePackageRequest, ServicePackage>();
            CreateMap<ServicePackage, ServicePackageResponse>();

            // ✅ Business
            CreateMap<CreateBusinessRequest, Business>();
            CreateMap<Business, CreateBusinessRequest>();

            // ✅ Category
            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<Category, CreateCategoryRequest>();

            // ✅ Consultation → ConsultationResponse
            CreateMap<Consultation, ConsultationResponse>()
                .ForMember(dest => dest.CustomerUserId, opt => opt.MapFrom(src =>
                    src.Customer != null ? src.Customer.UserId : null))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src =>
                    src.Customer != null ? $"{src.Customer.FirstName} {src.Customer.LastName}" : null))
                .ForMember(dest => dest.CustomerCountry, opt => opt.MapFrom(src =>
                    src.Customer != null ? src.Customer.CountryId : null))
                .ForMember(dest => dest.ConsultantUserId, opt => opt.MapFrom(src =>
                    src.Consultant != null ? src.Consultant.UserId : null))
                .ForMember(dest => dest.ConsultantName, opt => opt.MapFrom(src =>
                    src.Consultant != null ? $"{src.Consultant.FirstName} {src.Consultant.LastName}" : null))
                .ForMember(dest => dest.ServiceName, opt => opt.MapFrom(src =>
                    src.Service != null ? src.Service.ServiceName : null))
                .ForMember(dest => dest.PackageName, opt => opt.MapFrom(src =>
                    src.ServicePackage != null ? src.ServicePackage.PackageName : null))
                .ForMember(dest => dest.DurationMinutes, opt => opt.MapFrom(src =>
                    src.ServicePackage != null ? (int?)src.ServicePackage.DurationMinutes : null))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src =>
                    src.IsCustomOffer
                        ? (src.CustomPrice ?? 0)
                        : (src.ServicePackage != null ? src.ServicePackage.Price : 0)))

                // ── Session lifecycle ─────────────────────────────────────────
                .ForMember(dest => dest.StartedAt,             opt => opt.MapFrom(src => src.StartedAt))
                .ForMember(dest => dest.CompletionSummary,     opt => opt.MapFrom(src => src.CompletionSummary))
                .ForMember(dest => dest.CompletionFileUrl,     opt => opt.MapFrom(src => src.CompletionFileUrl))
                .ForMember(dest => dest.CompletionSubmittedAt, opt => opt.MapFrom(src => src.CompletionSubmittedAt))

                // ── Consultant no-show (reported by customer) ─────────────────
                .ForMember(dest => dest.NoShowRequestedAt,  opt => opt.MapFrom(src => src.NoShowRequestedAt))
                .ForMember(dest => dest.NoShowGraceHours,   opt => opt.MapFrom(src => src.NoShowGraceHours))
                .ForMember(dest => dest.NoShowProcessed,    opt => opt.MapFrom(src => src.NoShowProcessed))

                // ── Customer no-show (reported by consultant) ─────────────────
                .ForMember(dest => dest.CustomerNoShowRequestedAt, opt => opt.MapFrom(src => src.CustomerNoShowRequestedAt))
                .ForMember(dest => dest.CustomerNoShowGraceHours,  opt => opt.MapFrom(src => src.CustomerNoShowGraceHours))
                .ForMember(dest => dest.CustomerNoShowProcessed,   opt => opt.MapFrom(src => src.CustomerNoShowProcessed))

                // ── Disputes ──────────────────────────────────────────────────
                .ForMember(dest => dest.DisputeRaisedAt, opt => opt.MapFrom(src => src.DisputeRaisedAt))
                .ForMember(dest => dest.DisputeReason,   opt => opt.MapFrom(src => src.DisputeReason))
                .ForMember(dest => dest.DisputeStatus,   opt => opt.MapFrom(src => src.DisputeStatus))

                // ── Reschedule request ────────────────────────────────────────
                .ForMember(dest => dest.RescheduleRequestedAt,   opt => opt.MapFrom(src => src.RescheduleRequestedAt))
                .ForMember(dest => dest.RescheduleRequestReason, opt => opt.MapFrom(src => src.RescheduleRequestReason))

                // ── Manually set in ConsultationService ───────────────────────
                .ForMember(dest => dest.PendingAmount,              opt => opt.Ignore())
                .ForMember(dest => dest.CustomerTotalBookings,      opt => opt.Ignore())
                .ForMember(dest => dest.CustomerCompletedBookings,  opt => opt.Ignore())
                .ForMember(dest => dest.CustomerReviewCount,        opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAverageRating,      opt => opt.Ignore());

            // ✅ ConsultationBookingRequest → Consultation
            // Every Consultation field that is NOT in the booking request must be
            // explicitly ignored — otherwise AssertConfigurationIsValid() throws.
            CreateMap<ConsultationBookingRequest, Consultation>()
                .ForMember(dest => dest.Id,                        opt => opt.Ignore())
                .ForMember(dest => dest.CustomerId,                opt => opt.Ignore())
                .ForMember(dest => dest.ConsultantId,              opt => opt.Ignore())
                .ForMember(dest => dest.SendbirdChannelUrl,        opt => opt.Ignore())
                .ForMember(dest => dest.Status,                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,                 opt => opt.Ignore())
                .ForMember(dest => dest.EndAt,                     opt => opt.Ignore())
                .ForMember(dest => dest.IsCustomOffer,             opt => opt.Ignore())
                .ForMember(dest => dest.CustomPrice,               opt => opt.Ignore())
                .ForMember(dest => dest.CustomDurationMinutes,     opt => opt.Ignore())
                .ForMember(dest => dest.DeliverablesPath,          opt => opt.Ignore())
                .ForMember(dest => dest.CompletedAt,               opt => opt.Ignore())
                // ── Session lifecycle ─────────────────────────────────────────
                .ForMember(dest => dest.StartedAt,                 opt => opt.Ignore())
                .ForMember(dest => dest.CompletionSummary,         opt => opt.Ignore())
                .ForMember(dest => dest.CompletionFileUrl,         opt => opt.Ignore())
                .ForMember(dest => dest.CompletionSubmittedAt,     opt => opt.Ignore())
                // ── No-show fields ────────────────────────────────────────────
                .ForMember(dest => dest.ConsultantNoShowReported,  opt => opt.Ignore())
                .ForMember(dest => dest.CustomerNoShowReported,    opt => opt.Ignore())
                .ForMember(dest => dest.NoShowRequestedAt,         opt => opt.Ignore())
                .ForMember(dest => dest.NoShowGraceHours,          opt => opt.Ignore())
                .ForMember(dest => dest.NoShowProcessed,           opt => opt.Ignore())
                .ForMember(dest => dest.CustomerNoShowRequestedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerNoShowGraceHours,  opt => opt.Ignore())
                .ForMember(dest => dest.CustomerNoShowProcessed,   opt => opt.Ignore())
                // ── Disputes ──────────────────────────────────────────────────
                .ForMember(dest => dest.DisputeRaisedAt,           opt => opt.Ignore())
                .ForMember(dest => dest.DisputeReason,             opt => opt.Ignore())
                .ForMember(dest => dest.DisputeStatus,             opt => opt.Ignore())
                // ── Reschedule request ────────────────────────────────────────
                .ForMember(dest => dest.RescheduleRequestedAt,     opt => opt.Ignore())
                .ForMember(dest => dest.RescheduleRequestReason,   opt => opt.Ignore())
                // ── Navigation properties ─────────────────────────────────────
                .ForMember(dest => dest.Customer,                  opt => opt.Ignore())
                .ForMember(dest => dest.Consultant,                opt => opt.Ignore())
                .ForMember(dest => dest.Service,                   opt => opt.Ignore())
                .ForMember(dest => dest.ServicePackage,            opt => opt.Ignore());

            // ✅ ChatSession → ChatSessionResponse
            CreateMap<ChatSession, ChatSessionResponse>()
                .ForMember(dest => dest.CustomerUserId, opt => opt.MapFrom(src =>
                    src.Customer != null ? src.Customer.UserId : null))
                .ForMember(dest => dest.ConsultantUserId, opt => opt.MapFrom(src =>
                    src.Consultant != null ? src.Consultant.UserId : null))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src =>
                    src.Customer != null ? $"{src.Customer.FirstName} {src.Customer.LastName}" : null))
                .ForMember(dest => dest.ConsultantName, opt => opt.MapFrom(src =>
                    src.Consultant != null ? $"{src.Consultant.FirstName} {src.Consultant.LastName}" : null))
                .ForMember(dest => dest.ServiceName, opt => opt.MapFrom(src =>
                    src.Service != null ? src.Service.ServiceName : null));

            // ✅ CustomOffer
            CreateMap<CustomOfferRequest, CustomOffer>()
                .ForMember(dest => dest.Id,          opt => opt.Ignore())
                .ForMember(dest => dest.Status,      opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,   opt => opt.Ignore())
                .ForMember(dest => dest.AcceptedAt,  opt => opt.Ignore())
                .ForMember(dest => dest.ChatSession, opt => opt.Ignore())
                .ForMember(dest => dest.Service,     opt => opt.Ignore());

            CreateMap<CustomOffer, CustomOfferResponse>()
                .ForMember(dest => dest.ServiceName, opt => opt.MapFrom(src =>
                    src.Service != null ? src.Service.ServiceName : null));

            // ✅ Wallet
            CreateMap<Wallet, WalletResponse>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src =>
                    src.CustomerId.HasValue && src.Customer != null
                        ? src.Customer.UserId
                        : src.Consultant != null ? src.Consultant.UserId : null))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src =>
                    src.CustomerId.HasValue && src.Customer != null
                        ? $"{src.Customer.FirstName} {src.Customer.LastName}"
                        : src.Consultant != null ? $"{src.Consultant.FirstName} {src.Consultant.LastName}" : null))
                .ForMember(dest => dest.UserType, opt => opt.MapFrom(src =>
                    src.CustomerId.HasValue ? "Customer" : "Consultant"));
        }
    }
}