using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.BLL.Interfaces.IChatServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations
{
    public class ChatService : IChatService
    {
        private readonly IRepository<ChatSession> _chatSessionRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Service> _servicesRepo;
        private readonly IRepository<CustomOffer> _customOfferRepo;
        private readonly IRepository<Business> _businessRepo;
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISendbirdService _sendbirdService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;

        public ChatService(
            IUnitOfWork unitOfWork,
            ISendbirdService sendbirdService,
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper)
        {
            _unitOfWork          = unitOfWork;
            _chatSessionRepo     = _unitOfWork.GetRepository<ChatSession>();
            _customerRepo        = _unitOfWork.GetRepository<Customer>();
            _consultantRepo      = _unitOfWork.GetRepository<Consultant>();
            _servicesRepo        = _unitOfWork.GetRepository<Service>();
            _customOfferRepo     = _unitOfWork.GetRepository<CustomOffer>();
            _businessRepo        = _unitOfWork.GetRepository<Business>();
            _consultationRepo    = _unitOfWork.GetRepository<Consultation>();
            _walletRepo          = _unitOfWork.GetRepository<Wallet>();
            _sendbirdService     = sendbirdService;
            _httpContextAccessor = httpContextAccessor;
            _mapper              = mapper;
        }

        private string GetUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User is not authenticated.");
            return userId;
        }

        public async Task<ChatInitiateResponse> InitiateChatAsync(InitiateChatRequest request)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Customer not found.");
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == request.ConsultantUserId)
                             ?? throw new KeyNotFoundException("Consultant not found.");

            // Service + business validation — only if ServiceId was provided
            Service service = null;
            Business business = null;

            if (request.ServiceId.HasValue && request.ServiceId.Value > 0)
            {
                service = await _servicesRepo.GetSingleByAsync(
                    s => s.Id == request.ServiceId.Value,
                    include: q => q.Include(s => s.Business));
                if (service == null)
                    throw new KeyNotFoundException("Service not found.");

                business = await _businessRepo.GetSingleByAsync(
                    b => b.Id == service.BusinessId && b.ConsultantId == consultant.Id);
                if (business == null)
                    throw new UnauthorizedAccessException("This service does not belong to the specified consultant.");
            }

            // Look for existing chat session (with or without service)
            var existingChat = await _chatSessionRepo.GetSingleByAsync(cs =>
                cs.CustomerId   == customer.Id &&
                cs.ConsultantId == consultant.Id &&
                (request.ServiceId == null || cs.ServiceId == request.ServiceId.Value));

            string channelUrl;
            Guid chatSessionId;

            if (existingChat != null)
            {
                channelUrl    = existingChat.SendbirdChannelUrl;
                chatSessionId = existingChat.Id;
            }
            else
            {
                await _sendbirdService.EnsureSendbirdUserAsync(customer.UserId, $"{customer.FirstName} {customer.LastName}");
                await _sendbirdService.EnsureSendbirdUserAsync(consultant.UserId, $"{consultant.FirstName} {consultant.LastName}");
                channelUrl = await _sendbirdService.CreateGroupChannelAsync(customer.UserId, consultant.UserId);

                var chatSession = new ChatSession
                {
                    Id                 = Guid.NewGuid(),
                    CustomerId         = customer.Id,
                    ConsultantId       = consultant.Id,
                    ServiceId          = request.ServiceId ?? 0,
                    SendbirdChannelUrl = channelUrl,
                    CreatedAt          = DateTime.UtcNow
                };

                await _chatSessionRepo.AddAsync(chatSession);
                await _unitOfWork.SaveChangesAsync();
                chatSessionId = chatSession.Id;
            }

            // Send admin message (skip if no service)
            try
            {
                var msg = service != null
                    ? $"Chat initiated between {customer.FirstName} and {consultant.FirstName} regarding service: {service.ServiceName}."
                    : $"Chat initiated between {customer.FirstName} and {consultant.FirstName}.";

                var data = service != null
                    ? (object)new { ServiceId = service.Id, ServiceName = service.ServiceName }
                    : null;

                await _sendbirdService.SendAdminMessageAsync(channelUrl, msg, data);
            }
            catch { /* Don't fail if Sendbird admin message fails */ }

            return new ChatInitiateResponse
            {
                Success       = true,
                Message       = existingChat != null ? "Existing chat session retrieved." : "Chat session created successfully.",
                ChannelUrl    = channelUrl,
                ChatSessionId = chatSessionId.ToString()
            };
        }

        public async Task<CustomOfferResponse> CreateCustomOfferAsync(CustomOfferRequest request)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");

            var chatSession = await _chatSessionRepo.GetSingleByAsync(cs => cs.Id == request.ChatSessionId,
                include: q => q.Include(cs => cs.Service).Include(cs => cs.Consultant))
                ?? throw new KeyNotFoundException("Chat session not found.");

            if (chatSession.ConsultantId != consultant.Id)
                throw new UnauthorizedAccessException("You are not authorized to create an offer for this chat session.");

            var service = await _servicesRepo.GetSingleByAsync(s => s.Id == request.ServiceId,
                include: q => q.Include(s => s.Business))
                ?? throw new KeyNotFoundException("Service not found.");

            var business = await _businessRepo.GetSingleByAsync(
                b => b.Id == service.BusinessId && b.ConsultantId == consultant.Id)
                ?? throw new UnauthorizedAccessException("This service does not belong to the specified consultant.");

            var customOffer = _mapper.Map<CustomOffer>(request);
            customOffer.Status    = "Pending";
            customOffer.CreatedAt = DateTime.UtcNow;

            await _customOfferRepo.AddAsync(customOffer);
            await _unitOfWork.SaveChangesAsync();

            var message = $"Custom offer created for service: {service.ServiceName}. Price: ₦{customOffer.Price}. Description: {customOffer.Description}. Onsite: {customOffer.IncludesOnsiteVisit}. Scheduled: {customOffer.ScheduledAt:yyyy-MM-dd HH:mm}.";
            var offerData = new { OfferId = customOffer.Id, ServiceId = service.Id, ServiceName = service.ServiceName, customOffer.Price, customOffer.Description, customOffer.IncludesOnsiteVisit, customOffer.ScheduledAt };
            await _sendbirdService.SendAdminMessageAsync(chatSession.SendbirdChannelUrl, message, offerData);

            return _mapper.Map<CustomOfferResponse>(customOffer);
        }

        public async Task<CustomOfferResponse> AcceptCustomOfferAsync(Guid offerId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                           ?? throw new UnauthorizedAccessException("Customer not found.");

            var customOffer = await _customOfferRepo.GetSingleByAsync(co => co.Id == offerId,
                include: q => q
                    .Include(co => co.ChatSession).ThenInclude(cs => cs.Customer)
                    .Include(co => co.ChatSession).ThenInclude(cs => cs.Consultant)
                    .Include(co => co.Service))
                ?? throw new KeyNotFoundException("Custom offer not found.");

            if (customOffer.ChatSession.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You are not authorized to accept this offer.");
            if (customOffer.Status != "Pending")
                throw new InvalidOperationException("Only pending offers can be accepted.");
            if (!customOffer.ScheduledAt.HasValue)
                throw new InvalidOperationException("Custom offer does not have a scheduled time.");
            if (customOffer.DurationMinutes <= 0)
                throw new InvalidOperationException("Custom offer does not have a valid duration.");

            var isSlotTaken = await _consultationRepo.AnyAsync(c =>
                c.ConsultantId == customOffer.ChatSession.ConsultantId &&
                c.ScheduledAt  == customOffer.ScheduledAt.Value);
            if (isSlotTaken)
                throw new InvalidOperationException("The proposed time slot is already booked.");

            var customerWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id);
            if (customerWallet == null || customerWallet.Balance < customOffer.Price)
                throw new InvalidOperationException("Insufficient wallet balance. Please top up your wallet.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                customerWallet.Balance    -= customOffer.Price;
                customerWallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(customerWallet);

                var consultation = new Consultation
                {
                    Id                    = Guid.NewGuid(),
                    CustomerId            = customOffer.ChatSession.CustomerId,
                    ConsultantId          = customOffer.ChatSession.ConsultantId,
                    ServiceId             = customOffer.ServiceId,
                    ServicePackageId      = null,
                    ScheduledAt           = customOffer.ScheduledAt.Value,
                    EndAt                 = customOffer.ScheduledAt.Value.AddMinutes(customOffer.DurationMinutes),
                    Status                = "Pending",
                    SendbirdChannelUrl    = customOffer.ChatSession.SendbirdChannelUrl,
                    CreatedAt             = DateTime.UtcNow,
                    IsCustomOffer         = true,
                    CustomPrice           = customOffer.Price,
                    CustomDurationMinutes = customOffer.DurationMinutes
                };
                await _consultationRepo.AddAsync(consultation);

                await _unitOfWork.GetRepository<PendingTransaction>().AddAsync(new PendingTransaction
                {
                    Id             = Guid.NewGuid(),
                    CustomerId     = customer.Id,
                    ConsultationId = consultation.Id,
                    Amount         = customOffer.Price,
                    Status         = "Held",
                    CreatedAt      = DateTime.UtcNow
                });

                await _unitOfWork.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    CustomerId                   = customer.Id,
                    ConsultantId                 = null,
                    Amount                       = -customOffer.Price,
                    PaystackTransactionReference = null,
                    TransactionType              = "CustomOfferPayment",
                    Status                       = "Completed",
                    CreatedAt                    = DateTime.UtcNow,
                    CompletedAt                  = DateTime.UtcNow
                });

                customOffer.Status     = "Accepted";
                customOffer.AcceptedAt = DateTime.UtcNow;
                _customOfferRepo.Update(customOffer);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                try
                {
                    await _sendbirdService.SendAdminMessageAsync(customOffer.ChatSession.SendbirdChannelUrl,
                        $"✅ Custom offer accepted for {customOffer.Service.ServiceName}. " +
                        $"Price: ₦{customOffer.Price:N2} (held in escrow). " +
                        $"Scheduled: {customOffer.ScheduledAt:yyyy-MM-dd HH:mm}.");
                }
                catch { /* Don't fail if Sendbird fails */ }

                return _mapper.Map<CustomOfferResponse>(customOffer);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw new Exception($"Failed to accept custom offer: {ex.Message}", ex);
            }
        }

        public async Task<CustomOfferResponse> RejectCustomOfferAsync(Guid offerId, string reason)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                           ?? throw new UnauthorizedAccessException("Customer not found.");

            var customOffer = await _customOfferRepo.GetSingleByAsync(co => co.Id == offerId,
                include: q => q
                    .Include(co => co.ChatSession).ThenInclude(cs => cs.Customer)
                    .Include(co => co.Service))
                ?? throw new KeyNotFoundException("Custom offer not found.");

            if (customOffer.ChatSession.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You are not authorized to reject this offer.");
            if (customOffer.Status != "Pending")
                throw new InvalidOperationException("Only pending offers can be rejected.");

            customOffer.Status = "Rejected";
            _customOfferRepo.Update(customOffer);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendAdminMessageAsync(customOffer.ChatSession.SendbirdChannelUrl,
                $"Custom offer rejected for service: {customOffer.Service.ServiceName}. Reason: {reason}.");

            return _mapper.Map<CustomOfferResponse>(customOffer);
        }

        public async Task<IEnumerable<ChatSessionResponse>> GetMyChatsAsync()
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                           ?? throw new UnauthorizedAccessException("Customer not found.");

            var chatSessions = await _chatSessionRepo.GetAllAsync(
                cs => cs.CustomerId == customer.Id,
                include: q => q
                    .Include(cs => cs.Customer)
                    .Include(cs => cs.Consultant)
                    .Include(cs => cs.Service));

            return _mapper.Map<IEnumerable<ChatSessionResponse>>(chatSessions);
        }

        public async Task<IEnumerable<ChatSessionResponse>> GetConsultantChatsAsync()
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");

            var chatSessions = await _chatSessionRepo.GetAllAsync(
                cs => cs.ConsultantId == consultant.Id,
                include: q => q
                    .Include(cs => cs.Customer)
                    .Include(cs => cs.Consultant)
                    .Include(cs => cs.Service));

            return _mapper.Map<IEnumerable<ChatSessionResponse>>(chatSessions);
        }
    }
}