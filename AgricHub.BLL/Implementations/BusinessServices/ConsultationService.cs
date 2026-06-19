// AgricHub.BLL/Implementations/BusinessServices/ConsultationService.cs

using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.BLL.Interfaces.IBusinessServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.BusinessServices
{
    public class ConsultationService : IConsultationService
    {
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Service> _servicesRepo;
        private readonly IRepository<ServicePackage> _servicePackageRepo;
        private readonly IRepository<Business> _businessRepo;
        private readonly IRepository<ChatSession> _chatSessionRepo;
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<PendingTransaction> _pendingTransactionRepo;
        private readonly IRepository<WalletTransaction> _walletTransactionRepo;
        private readonly IRepository<Review> _reviewRepo;
        private readonly ISendbirdService _sendbirdService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _env;
        private readonly IMapper _mapper;
        private readonly ILogger<ConsultationService> _logger;

        private const decimal CustomerNoShowPayoutPercentage = 0.5m;
        private const int GracePeriodMinutes = 15;
        private const int CompletionApprovalGraceHours = 72; // 3 days — auto-release if customer doesn't review

        public ConsultationService(
            IUnitOfWork unitOfWork,
            ISendbirdService sendbirdService,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment env,
            IMapper mapper,
            ILogger<ConsultationService> logger)
        {
            _unitOfWork             = unitOfWork;
            _consultationRepo       = _unitOfWork.GetRepository<Consultation>();
            _customerRepo           = _unitOfWork.GetRepository<Customer>();
            _consultantRepo         = _unitOfWork.GetRepository<Consultant>();
            _servicesRepo           = _unitOfWork.GetRepository<Service>();
            _servicePackageRepo     = _unitOfWork.GetRepository<ServicePackage>();
            _businessRepo           = _unitOfWork.GetRepository<Business>();
            _chatSessionRepo        = _unitOfWork.GetRepository<ChatSession>();
            _walletRepo             = _unitOfWork.GetRepository<Wallet>();
            _pendingTransactionRepo = _unitOfWork.GetRepository<PendingTransaction>();
            _walletTransactionRepo  = _unitOfWork.GetRepository<WalletTransaction>();
            _reviewRepo             = _unitOfWork.GetRepository<Review>();
            _sendbirdService        = sendbirdService;
            _httpContextAccessor    = httpContextAccessor;
            _env                    = env;
            _mapper                 = mapper;
            _logger                 = logger;
        }

        private string GetUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User is not authenticated.");
            return userId;
        }

        private async Task EnsureConsultantOwnershipAsync(Consultation consultation, string userId)
        {
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant == null || consultant.Id != consultation.ConsultantId)
                throw new UnauthorizedAccessException("You are not authorized to manage this consultation.");
        }

        private async Task EnsureCustomerOrConsultantAsync(Consultation consultation, string userId)
        {
            var full = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultation.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant))
                ?? throw new KeyNotFoundException("Consultation not found.");
            if (full.Customer?.UserId != userId && full.Consultant?.UserId != userId)
                throw new UnauthorizedAccessException("You are not authorized to perform this action.");
        }

        private Task<PendingTransaction?> FindEscrowAsync(Guid consultationId) =>
            _pendingTransactionRepo.GetSingleByAsync(
                pt => pt.ConsultationId == consultationId && pt.Status == "Held");

        /// <summary>Saves a consultant's completion-proof file under {ContentRoot}/Resources/Completions and returns its relative path.</summary>
        private async Task<string> SaveCompletionFileAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.ContentRootPath, "Resources", "Completions");
            Directory.CreateDirectory(dir);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(dir, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Relative path — served at /Resources/Completions/{file} per the
            // app.UseStaticFiles(...) mapping for the Resources folder in Program.cs
            return Path.Combine("Resources", "Completions", fileName).Replace("\\", "/");
        }

        /// <summary>The agreed price for this consultation — custom offer price, or the service package price.</summary>
        private static decimal ResolvePrice(Consultation consultation) =>
            consultation.IsCustomOffer
                ? (consultation.CustomPrice ?? 0)
                : (consultation.ServicePackage?.Price ?? 0);

        // ── Book ────────────────────────────────────────────────────────────────

        public async Task<ConsultationResponse> BookConsultationAsync(ConsultationBookingRequest dto)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                           ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == dto.ConsultantUserId)
                             ?? throw new KeyNotFoundException("Consultant not found.");

            var service = await _servicesRepo.GetSingleByAsync(s => s.Id == dto.ServiceId,
                include: q => q.Include(s => s.Business).Include(s => s.Packages))
                ?? throw new KeyNotFoundException("Service not found.");

            var package = service.Packages.FirstOrDefault(p => p.Id == dto.ServicePackageId)
                          ?? throw new KeyNotFoundException("Service package not found.");

            var business = await _businessRepo.GetSingleByAsync(
                b => b.Id == service.BusinessId && b.ConsultantId == consultant.Id)
                ?? throw new UnauthorizedAccessException("This service does not belong to the specified consultant.");

            // ── Validation ──────────────────────────────────────────────────────

            // 1. Reject past dates
            if (dto.ScheduledAt < DateTime.UtcNow)
                throw new InvalidOperationException("Cannot book a consultation in the past. Please choose a future date and time.");

            // 2. Require at least 1 hour advance notice
            if (dto.ScheduledAt < DateTime.UtcNow.AddHours(1))
                throw new InvalidOperationException("Bookings must be made at least 1 hour in advance.");

            // 3. Consultant conflict — check for any ACTIVE booking that overlaps this time window
            var sessionEnd = dto.ScheduledAt.AddMinutes(package.DurationMinutes);
            var consultantConflict = await _consultationRepo.AnyAsync(c =>
                c.ConsultantId == consultant.Id &&
                (c.Status == "Pending" || c.Status == "Approved" || c.Status == "InProgress") &&
                c.ScheduledAt < sessionEnd &&
                c.EndAt > dto.ScheduledAt);
            if (consultantConflict)
                throw new InvalidOperationException("This consultant already has a booking during that time. Please choose a different slot.");

            // 4. Customer conflict — prevent the customer from double-booking themselves
            var customerConflict = await _consultationRepo.AnyAsync(c =>
                c.CustomerId == customer.Id &&
                (c.Status == "Pending" || c.Status == "Approved" || c.Status == "InProgress") &&
                c.ScheduledAt < sessionEnd &&
                c.EndAt > dto.ScheduledAt);
            if (customerConflict)
                throw new InvalidOperationException("You already have a booking during this time. Please choose a different slot.");

            var customerWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id)
                                 ?? throw new InvalidOperationException("Customer wallet not found.");
            if (customerWallet.Balance < package.Price)
                throw new InvalidOperationException("Insufficient wallet balance. Please top up your wallet.");

            customerWallet.Balance    -= package.Price;
            customerWallet.LastUpdated = DateTime.UtcNow;
            _walletRepo.Update(customerWallet);

            var consultation = new Consultation
            {
                Id               = Guid.NewGuid(),
                CustomerId       = customer.Id,
                ConsultantId     = consultant.Id,
                ServiceId        = dto.ServiceId,
                ServicePackageId = dto.ServicePackageId,
                ScheduledAt      = dto.ScheduledAt,
                EndAt            = dto.ScheduledAt.AddMinutes(package.DurationMinutes),
                Notes            = dto.Notes,
                Status           = "Pending",
                CreatedAt        = DateTime.UtcNow,
                IsCustomOffer    = false,
            };

            var existingChat = await _chatSessionRepo.GetSingleByAsync(cs =>
                cs.CustomerId == customer.Id && cs.ConsultantId == consultant.Id && cs.ServiceId == dto.ServiceId);

            if (existingChat != null)
            {
                consultation.SendbirdChannelUrl = existingChat.SendbirdChannelUrl;
            }
            else
            {
                await _sendbirdService.EnsureSendbirdUserAsync(customer.UserId, $"{customer.FirstName} {customer.LastName}");
                await _sendbirdService.EnsureSendbirdUserAsync(consultant.UserId, $"{consultant.FirstName} {consultant.LastName}");
                var channelUrl = await _sendbirdService.CreateGroupChannelAsync(customer.UserId, consultant.UserId);
                consultation.SendbirdChannelUrl = channelUrl;
                await _chatSessionRepo.AddAsync(new ChatSession
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    ConsultantId = consultant.Id,
                    ServiceId = dto.ServiceId,
                    SendbirdChannelUrl = channelUrl,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _consultationRepo.AddAsync(consultation);
            await _pendingTransactionRepo.AddAsync(new PendingTransaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                ConsultationId = consultation.Id,
                Amount = package.Price,
                Status = "Held",
                CreatedAt = DateTime.UtcNow
            });
            await _walletTransactionRepo.AddAsync(new WalletTransaction
            {
                CustomerId = customer.Id,
                ConsultantId = null,
                Amount = -package.Price,
                TransactionType = "ConsultationPayment",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync();

            var saved = await _consultationRepo.GetSingleByAsync(c => c.Id == consultation.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                               .Include(c => c.Service).Include(c => c.ServicePackage));
            var escrow = await FindEscrowAsync(consultation.Id);
            var response = _mapper.Map<ConsultationResponse>(saved);
            response.PendingAmount = escrow?.Amount ?? 0;
            response.Price = ResolvePrice(saved!);

            var message = string.IsNullOrWhiteSpace(dto.Notes)
                ? $"Booking request for {service.ServiceName} ({package.PackageName}). ${package.Price} held in escrow."
                : $"Booking request for {service.ServiceName} ({package.PackageName}). ${package.Price} held in escrow. Notes: {dto.Notes}";

            await _sendbirdService.SendMessageAsync(saved!.SendbirdChannelUrl, customer.UserId, message, false,
                new { ServiceId = service.Id, ServiceName = service.ServiceName, PackageId = package.Id, PackageName = package.PackageName, Price = package.Price });

            await _sendbirdService.SendNotificationAsync(consultant.UserId,
                $"📋 New booking from {customer.FirstName} {customer.LastName} · {service.ServiceName} on {dto.ScheduledAt:MMM d, h:mm tt}",
                "booking_request");

            _logger.LogInformation("[Book] New booking {Id} · Customer: {Customer} · Service: {Service} · ${Amount} held in escrow.",
                consultation.Id, customer.UserId, service.ServiceName, package.Price);

            return response;
        }

        // ── Approve ─────────────────────────────────────────────────────────────

        public async Task<ConsultationResponse> ApproveConsultationAsync(Guid consultationId, string? notes = null)
        {
            var userId = GetUserId();
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Customer).Include(c => c.Consultant))
                ?? throw new KeyNotFoundException("Consultation not found.");

            await EnsureConsultantOwnershipAsync(consultation, userId);
            if (consultation.Status != "Pending")
                throw new InvalidOperationException("Only pending consultations can be approved.");

            var escrow = await FindEscrowAsync(consultationId)
                ?? throw new InvalidOperationException("No escrow found for this consultation.");

            if (string.IsNullOrEmpty(consultation.SendbirdChannelUrl))
            {
                await _sendbirdService.EnsureSendbirdUserAsync(consultation.Customer.UserId, $"{consultation.Customer.FirstName} {consultation.Customer.LastName}");
                await _sendbirdService.EnsureSendbirdUserAsync(consultation.Consultant.UserId, $"{consultation.Consultant.FirstName} {consultation.Consultant.LastName}");
                consultation.SendbirdChannelUrl = await _sendbirdService.CreateGroupChannelAsync(consultation.Customer.UserId, consultation.Consultant.UserId);
            }

            consultation.Status = "Approved";
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var amount = ResolvePrice(consultation);
            var msg = string.IsNullOrEmpty(notes)
                ? $"✅ Booking approved · {consultation.Service.ServiceName} · ${amount} held in escrow."
                : $"✅ Booking approved · {consultation.Service.ServiceName} · ${amount} held in escrow. Notes: {notes}";
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, msg);

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"✅ Your booking for {consultation.Service.ServiceName} is confirmed · {consultation.ScheduledAt:MMM d, h:mm tt}",
                "booking_confirmed");

            _logger.LogInformation("[Approve] Consultation {Id} approved by consultant {Consultant}.", consultationId, userId);

            var resp = _mapper.Map<ConsultationResponse>(consultation);
            resp.PendingAmount = escrow.Amount;
            resp.Price = amount;
            return resp;
        }

        // ── Reject ──────────────────────────────────────────────────────────────

        public async Task<ConsultationResponse> RejectConsultationAsync(Guid consultationId, string reason)
        {
            var userId = GetUserId();
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("Consultation not found.");

            await EnsureConsultantOwnershipAsync(consultation, userId);
            if (consultation.Status != "Pending")
                throw new InvalidOperationException("Only pending consultations can be rejected.");

            var escrow = await FindEscrowAsync(consultationId);
            if (escrow != null)
            {
                var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == consultation.CustomerId)
                             ?? throw new InvalidOperationException("Customer wallet not found.");
                wallet.Balance    += escrow.Amount;
                wallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(wallet);
                escrow.Status     = "Refunded";
                escrow.ResolvedAt = DateTime.UtcNow;
                _pendingTransactionRepo.Update(escrow);
                await _walletTransactionRepo.AddAsync(new WalletTransaction
                {
                    CustomerId = consultation.CustomerId,
                    ConsultantId = null,
                    Amount = escrow.Amount,
                    TransactionType = "ConsultationRefund",
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
            }

            consultation.Status = "Rejected";
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"❌ Booking declined. Reason: {reason}. ${escrow?.Amount ?? 0} refunded.");

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"❌ Your booking for {consultation.Service.ServiceName} was declined · ${escrow?.Amount ?? 0} refunded",
                "booking_rejected");

            _logger.LogInformation("[Reject] Consultation {Id} rejected · Refunded ${Amount}.", consultationId, escrow?.Amount ?? 0);

            var response = _mapper.Map<ConsultationResponse>(consultation);
            response.PendingAmount = escrow?.Amount ?? 0;
            response.Price = ResolvePrice(consultation);
            return response;
        }

        // ── Start ───────────────────────────────────────────────────────────────

        public async Task<ConsultationResponse> StartConsultationAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage).Include(c => c.Consultant))
                ?? throw new KeyNotFoundException("Consultation not found.");

            await EnsureConsultantOwnershipAsync(consultation, userId);

            if (consultation.Status != "Approved")
                throw new InvalidOperationException("Only approved consultations can be started.");

            // ── 30-minute start window ─────────────────────────────────────────────
            var canStartFrom = consultation.ScheduledAt.AddMinutes(-30);
            if (DateTime.UtcNow < canStartFrom)
                throw new InvalidOperationException(
                    $"Sessions can only be started within 30 minutes of the scheduled time. " +
                    $"You can start from {canStartFrom:MMM d, h:mm tt} UTC.");

            var escrow = await FindEscrowAsync(consultationId)
                ?? throw new InvalidOperationException("No escrow found.");

            consultation.Status            = "InProgress";
            consultation.StartedAt         = DateTime.UtcNow;
            consultation.NoShowRequestedAt = null;
            consultation.NoShowGraceHours  = null;
            consultation.NoShowProcessed   = false;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var amount = ResolvePrice(consultation);
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"🚀 Session started · {consultation.Service.ServiceName} · ${amount} held in escrow.");

            var customer = await _customerRepo.GetSingleByAsync(c => c.Id == consultation.CustomerId);
            if (customer != null)
                await _sendbirdService.SendNotificationAsync(customer.UserId,
                    $"🚀 Your session for {consultation.Service.ServiceName} has started", "session_started");

            _logger.LogInformation("[Start] Consultation {Id} started at {StartedAt} UTC · Service: {Service}.",
                consultationId, consultation.StartedAt, consultation.Service.ServiceName);

            var resp = _mapper.Map<ConsultationResponse>(consultation);
            resp.PendingAmount = escrow.Amount;
            resp.Price = amount;
            return resp;
        }

        public async Task ProcessExpiredNoShowForConsultationAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId &&
                     c.CustomerId == customer.Id &&
                     c.NoShowRequestedAt.HasValue &&
                     !c.NoShowProcessed,
                include: q => q.Include(c => c.Customer)
                               .Include(c => c.Consultant)
                               .Include(c => c.Service))
                ?? throw new KeyNotFoundException("No pending no-show request found.");

            var expiresAt = consultation.NoShowRequestedAt!.Value
                .AddHours(consultation.NoShowGraceHours ?? 5);

            if (DateTime.UtcNow < expiresAt)
                throw new InvalidOperationException(
                    $"Grace period has not expired yet. Expires at {expiresAt:HH:mm} UTC.");

            await ReportConsultantNoShowAsync(consultationId);
        }

        // ── Submit completion (consultant submits proof for customer review) ────

        public async Task<ConsultationResponse> SubmitCompletionAsync(Guid consultationId, string summary, IFormFile file)
        {
            var userId = GetUserId();
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("Consultation not found.");

            await EnsureConsultantOwnershipAsync(consultation, userId);

            // Accept InProgress and OverdueReview
            if (consultation.Status != "InProgress" && consultation.Status != "OverdueReview")
                throw new InvalidOperationException("Only in-progress or overdue consultations can be submitted for review.");

            if (file == null || file.Length == 0)
                throw new InvalidOperationException("A proof file is required to submit for review.");

            if (string.IsNullOrWhiteSpace(summary))
                throw new InvalidOperationException("A summary of the work completed is required.");

            var fileUrl = await SaveCompletionFileAsync(file);

            consultation.CompletionSummary      = summary;
            consultation.CompletionFileUrl      = fileUrl;
            consultation.CompletionSubmittedAt  = DateTime.UtcNow;
            consultation.Status                 = "PendingApproval";
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var escrow = await FindEscrowAsync(consultationId);

            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"📄 {consultation.Consultant.FirstName} submitted the work for review · {consultation.Service.ServiceName}. " +
                $"Please review and approve to release the ${ResolvePrice(consultation)} held in escrow. " +
                $"If not reviewed within {CompletionApprovalGraceHours / 24} days, it will release automatically.");

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"📄 {consultation.Consultant.FirstName} {consultation.Consultant.LastName} submitted work for {consultation.Service.ServiceName} · Please review and approve",
                "completion_submitted");

            _logger.LogInformation("[Submit] Consultation {Id} submitted for review by consultant {Consultant} · File: {File}.",
                consultationId, userId, fileUrl);

            var resp = _mapper.Map<ConsultationResponse>(consultation);
            resp.PendingAmount = escrow?.Amount ?? 0;
            resp.Price = ResolvePrice(consultation);
            return resp;
        }

        // ── Approve completion (customer reviews proof → escrow released) ───────

        public async Task<ConsultationResponse> ApproveCompletionAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (consultation.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You are not authorized to approve this consultation.");

            if (consultation.Status != "PendingApproval")
                throw new InvalidOperationException("Only consultations awaiting your review can be approved.");

            var escrow = await FindEscrowAsync(consultationId)
                ?? throw new InvalidOperationException("No escrow found.");

            var consultantWallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultation.ConsultantId)
                                   ?? throw new InvalidOperationException("Consultant wallet not found.");
            consultantWallet.Balance    += escrow.Amount;
            consultantWallet.LastUpdated = DateTime.UtcNow;
            _walletRepo.Update(consultantWallet);

            escrow.Status     = "Released";
            escrow.ResolvedAt = DateTime.UtcNow;
            _pendingTransactionRepo.Update(escrow);

            await _walletTransactionRepo.AddAsync(new WalletTransaction
            {
                CustomerId = null,
                ConsultantId = consultation.ConsultantId,
                Amount = escrow.Amount,
                TransactionType = "ConsultationPayout",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });

            consultation.Status      = "Completed";
            consultation.CompletedAt = DateTime.UtcNow;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var amount = ResolvePrice(consultation);
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"✅ Customer approved the submission · {consultation.Service.ServiceName} · ${amount} released to consultant wallet.");

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"✅ Session with {consultation.Consultant.FirstName} {consultation.Consultant.LastName} is complete · Please leave a review",
                "session_complete");

            await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                $"💰 ${escrow.Amount} released to your wallet · {consultation.Service.ServiceName}",
                "payout_released");

            _logger.LogInformation("[ApproveCompletion] Consultation {Id} approved by customer · ${Amount} released to consultant.",
                consultationId, escrow.Amount);

            var resp = _mapper.Map<ConsultationResponse>(consultation);
            resp.PendingAmount = 0;
            resp.Price = amount;
            return resp;
        }

        // ── Dispute (customer rejects the submission — escrow frozen, sent to admin) ──

        public async Task<ConsultationResponse> DisputeCompletionAsync(Guid consultationId, string reason)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId && c.CustomerId == customer.Id,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (consultation.Status != "PendingApproval")
                throw new InvalidOperationException("Only submissions awaiting your review can be disputed.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Please describe the issue before raising a dispute.");

            consultation.Status          = "Disputed";
            consultation.DisputeRaisedAt = DateTime.UtcNow;
            consultation.DisputeReason   = reason;
            consultation.DisputeStatus   = "Open";
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var escrow = await FindEscrowAsync(consultationId);

            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"🚩 Customer raised a dispute on this submission · Reason: {reason}. Escrow remains held pending review by our team — the 3-day auto-release is paused.");

            await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                $"🚩 A dispute was raised on your submission for {consultation.Service.ServiceName} · Our team will review and contact you",
                "completion_disputed");

            _logger.LogWarning("[Dispute] Consultation {Id} disputed by customer · Reason: {Reason} · Escrow ${Amount} frozen.",
                consultationId, reason, escrow?.Amount ?? 0);

            var resp = _mapper.Map<ConsultationResponse>(consultation);
            resp.PendingAmount = escrow?.Amount ?? 0;
            resp.Price = ResolvePrice(consultation);
            return resp;
        }

        // ── Admin: list disputes ────────────────────────────────────────────────

        public async Task<IEnumerable<ConsultationResponse>> GetDisputedConsultationsAsync(string? statusFilter = null)
        {
            IEnumerable<Consultation> consultations;
            if (string.IsNullOrEmpty(statusFilter) || statusFilter == "all")
            {
                consultations = await _consultationRepo.GetAllAsync(c => c.DisputeRaisedAt.HasValue,
                    include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                                   .Include(c => c.Service).Include(c => c.ServicePackage));
            }
            else if (statusFilter == "open")
            {
                consultations = await _consultationRepo.GetAllAsync(c => c.Status == "Disputed" && c.DisputeStatus == "Open",
                    include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                                   .Include(c => c.Service).Include(c => c.ServicePackage));
            }
            else
            {
                consultations = await _consultationRepo.GetAllAsync(c => c.DisputeStatus == statusFilter,
                    include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                                   .Include(c => c.Service).Include(c => c.ServicePackage));
            }

            var list = consultations.OrderByDescending(c => c.DisputeRaisedAt).ToList();
            var responses = _mapper.Map<IEnumerable<ConsultationResponse>>(list).ToList();
            for (int i = 0; i < responses.Count; i++)
            {
                var escrow = await FindEscrowAsync(responses[i].Id);
                responses[i].PendingAmount = escrow?.Amount ?? 0;
                responses[i].Price = ResolvePrice(list[i]);
            }
            return responses;
        }

        // ── Admin: resolve a dispute ────────────────────────────────────────────
        // resolution: "Release" (full escrow → consultant wallet) or "Refund" (full escrow → customer wallet)

        public async Task ResolveDisputeAsync(Guid consultationId, string resolution, string? notes)
        {
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId && c.Status == "Disputed",
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("No open dispute found for this consultation.");

            var escrow = await FindEscrowAsync(consultationId)
                ?? throw new InvalidOperationException("No escrow found for this consultation.");

            var amount = escrow.Amount;

            if (resolution == "Release")
            {
                var consultantWallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultation.ConsultantId)
                    ?? throw new InvalidOperationException("Consultant wallet not found.");
                consultantWallet.Balance    += amount;
                consultantWallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(consultantWallet);

                escrow.Status     = "Released";
                escrow.ResolvedAt = DateTime.UtcNow;
                _pendingTransactionRepo.Update(escrow);

                await _walletTransactionRepo.AddAsync(new WalletTransaction
                {
                    CustomerId = null,
                    ConsultantId = consultation.ConsultantId,
                    Amount = amount,
                    TransactionType = "DisputeResolvedPayout",
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });

                consultation.Status      = "Completed";
                consultation.CompletedAt = DateTime.UtcNow;

                await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                    $"✅ Dispute resolved by our team — submission approved. ${amount} released to consultant."
                    + (string.IsNullOrWhiteSpace(notes) ? "" : $" Notes: {notes}"));
                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                    $"✅ Dispute resolved in your favor · ${amount} released to your wallet · {consultation.Service.ServiceName}",
                    "payout_released");
                await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                    $"Our team reviewed your dispute and approved the consultant's work · {consultation.Service.ServiceName}",
                    "session_complete");
            }
            else if (resolution == "Refund")
            {
                var customerWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == consultation.CustomerId)
                    ?? throw new InvalidOperationException("Customer wallet not found.");
                customerWallet.Balance    += amount;
                customerWallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(customerWallet);

                escrow.Status     = "Refunded";
                escrow.ResolvedAt = DateTime.UtcNow;
                _pendingTransactionRepo.Update(escrow);

                await _walletTransactionRepo.AddAsync(new WalletTransaction
                {
                    CustomerId = consultation.CustomerId,
                    ConsultantId = null,
                    Amount = amount,
                    TransactionType = "DisputeResolvedRefund",
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });

                consultation.Status = "Cancelled";

                await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                    $"⚠️ Dispute resolved by our team — submission rejected. ${amount} refunded to customer."
                    + (string.IsNullOrWhiteSpace(notes) ? "" : $" Notes: {notes}"));
                await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                    $"💰 Dispute resolved · ${amount} refunded to your wallet · {consultation.Service.ServiceName}",
                    "booking_cancelled");
                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                    $"Our team reviewed the dispute and refunded the customer for {consultation.Service.ServiceName}",
                    "booking_cancelled");
            }
            else
            {
                throw new InvalidOperationException("Resolution must be 'Release' or 'Refund'.");
            }

            consultation.DisputeStatus = resolution == "Release" ? "ResolvedReleased" : "ResolvedRefunded";
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("[ResolveDispute] Consultation {Id} dispute resolved as {Resolution} · ${Amount}.",
                consultationId, resolution, amount);
        }

        // ── Auto-release if customer doesn't review in time (called by frontend) ─

        public async Task ProcessExpiredApprovalForConsultationAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId &&
                     c.ConsultantId == consultant.Id &&
                     c.Status == "PendingApproval" &&
                     c.CompletionSubmittedAt.HasValue,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("No pending approval found for this consultation.");

            var expiresAt = consultation.CompletionSubmittedAt!.Value.AddHours(CompletionApprovalGraceHours);
            if (DateTime.UtcNow < expiresAt)
                throw new InvalidOperationException(
                    $"Approval window has not expired yet. Expires at {expiresAt:yyyy-MM-dd HH:mm} UTC.");

            var escrow = await FindEscrowAsync(consultationId)
                ?? throw new InvalidOperationException("No escrow found.");

            var consultantWallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultation.ConsultantId)
                                   ?? throw new InvalidOperationException("Consultant wallet not found.");
            consultantWallet.Balance    += escrow.Amount;
            consultantWallet.LastUpdated = DateTime.UtcNow;
            _walletRepo.Update(consultantWallet);

            escrow.Status     = "Released";
            escrow.ResolvedAt = DateTime.UtcNow;
            _pendingTransactionRepo.Update(escrow);

            await _walletTransactionRepo.AddAsync(new WalletTransaction
            {
                CustomerId = null,
                ConsultantId = consultation.ConsultantId,
                Amount = escrow.Amount,
                TransactionType = "ConsultationPayout",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });

            consultation.Status      = "Completed";
            consultation.CompletedAt = DateTime.UtcNow;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var amount = ResolvePrice(consultation);
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"⏰ Review window expired · ${amount} automatically released to consultant wallet · {consultation.Service.ServiceName}.");

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"⏰ Your {CompletionApprovalGraceHours / 24}-day review window for {consultation.Service.ServiceName} expired · payment was released automatically",
                "session_complete");

            await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                $"💰 ${escrow.Amount} automatically released to your wallet (review window expired) · {consultation.Service.ServiceName}",
                "payout_released");

            _logger.LogInformation("[ApprovalExpiry] Consultation {Id} auto-released ${Amount} to consultant (frontend trigger).",
                consultationId, escrow.Amount);
        }

        // ── Cancel ──────────────────────────────────────────────────────────────

        public async Task<ConsultationResponse> CancelConsultationAsync(Guid consultationId, string? reason = null)
        {
            var userId = GetUserId();
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Customer).Include(c => c.Consultant))
                ?? throw new KeyNotFoundException("Consultation not found.");

            await EnsureCustomerOrConsultantAsync(consultation, userId);
            if (consultation.Status is "Completed" or "Rejected" or "PendingApproval")
                throw new InvalidOperationException("Cannot cancel a completed, rejected, or pending-approval consultation.");

            var isConsultant = (await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)) != null;
            if (isConsultant && consultation.Status == "Approved" && consultation.ScheduledAt <= DateTime.UtcNow)
                throw new InvalidOperationException(
                    "You cannot cancel after the scheduled start time. Please start the session or request a reschedule. " +
                    "If you did not attend, the customer may report a no-show.");

            var escrow = await FindEscrowAsync(consultationId);
            if (escrow != null)
            {
                var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == consultation.CustomerId)
                             ?? throw new InvalidOperationException("Customer wallet not found.");
                wallet.Balance    += escrow.Amount;
                wallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(wallet);
                escrow.Status     = "Refunded";
                escrow.ResolvedAt = DateTime.UtcNow;
                _pendingTransactionRepo.Update(escrow);
                await _walletTransactionRepo.AddAsync(new WalletTransaction
                {
                    CustomerId = consultation.CustomerId,
                    ConsultantId = null,
                    Amount = escrow.Amount,
                    TransactionType = "ConsultationRefund",
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
            }

            consultation.Status = "Cancelled";
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var msg = string.IsNullOrEmpty(reason)
                ? $"⚠️ Booking cancelled. ${escrow?.Amount ?? 0} refunded."
                : $"⚠️ Booking cancelled. Reason: {reason}. ${escrow?.Amount ?? 0} refunded.";
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, msg);

            var cancelledByCustomer = (await _customerRepo.GetSingleByAsync(c => c.UserId == userId)) != null;
            if (cancelledByCustomer && consultation.Consultant != null)
                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                    $"⚠️ Booking cancelled by customer · {consultation.Service?.ServiceName}", "booking_cancelled");
            else if (!cancelledByCustomer && consultation.Customer != null)
                await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                    $"⚠️ Booking cancelled by consultant · ${escrow?.Amount ?? 0} refunded", "booking_cancelled");

            _logger.LogInformation("[Cancel] Consultation {Id} cancelled by {CancelledBy} · Refunded ${Amount}.",
                consultationId, cancelledByCustomer ? "customer" : "consultant", escrow?.Amount ?? 0);

            var response = _mapper.Map<ConsultationResponse>(consultation);
            response.PendingAmount = 0;
            response.Price = ResolvePrice(consultation);
            return response;
        }

        // ── No-show request (customer) ────────────────────────────────────────

        public async Task RequestNoShowAsync(Guid consultationId, int graceHours)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId && c.CustomerId == customer.Id,
                include: q => q.Include(c => c.Consultant).Include(c => c.Service))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (!new[] { "Approved", "InProgress", "OverdueReview" }.Contains(consultation.Status))
                throw new InvalidOperationException("No-show can only be requested for approved or in-progress sessions.");

            if (consultation.NoShowRequestedAt.HasValue && !consultation.NoShowProcessed)
                throw new InvalidOperationException("A no-show request is already pending for this consultation.");

            var gracePeriodEnd = consultation.ScheduledAt.AddMinutes(GracePeriodMinutes);
            if (DateTime.UtcNow < gracePeriodEnd)
                throw new InvalidOperationException($"Please wait until {gracePeriodEnd:HH:mm} UTC before reporting a no-show.");

            graceHours = Math.Clamp(graceHours, 1, 48);

            consultation.NoShowRequestedAt = DateTime.UtcNow;
            consultation.NoShowGraceHours  = graceHours;
            consultation.NoShowProcessed   = false;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendNotificationAsync(
                consultation.Consultant.UserId,
                $"⚠️ No-show reported by customer · {consultation.Service.ServiceName} · You have {graceHours}h to respond",
                "noshow_request");

            await _sendbirdService.SendAdminMessageAsync(
                consultation.SendbirdChannelUrl,
                $"⚠️ Customer has reported a no-show. Consultant has {graceHours} hours to respond (click 'I'm here' on your schedule). If no response, escrow will be automatically refunded.");

            _logger.LogInformation("[NoShowRequest] Customer reported no-show for consultation {Id} · Grace: {Hours}h.", consultationId, graceHours);
        }

        // ── Dismiss no-show (consultant says "I'm here") ─────────────────────

        public async Task DismissNoShowRequestAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId && c.ConsultantId == consultant.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Service))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (!consultation.NoShowRequestedAt.HasValue)
                throw new InvalidOperationException("No pending no-show request for this consultation.");

            consultation.NoShowRequestedAt = null;
            consultation.NoShowGraceHours  = null;
            consultation.NoShowProcessed   = false;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendNotificationAsync(
                consultation.Customer.UserId,
                $"✅ {consultant.FirstName} confirmed they are present · {consultation.Service.ServiceName} · Session is resuming",
                "noshow_dismissed");

            await _sendbirdService.SendAdminMessageAsync(
                consultation.SendbirdChannelUrl,
                $"✅ Consultant confirmed presence. No-show request dismissed. Session continues.");

            _logger.LogInformation("[DismissNoShow] Consultant {Consultant} dismissed no-show for consultation {Id}.", userId, consultationId);
        }

        // ── Process expired no-shows (background sweep) ───────────────────────

        public async Task ProcessExpiredNoShowsAsync()
        {
            var all = await _consultationRepo.GetAllAsync(
                c => c.NoShowRequestedAt.HasValue && !c.NoShowProcessed,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant).Include(c => c.Service));

            if (!all.Any()) { _logger.LogDebug("[NoShow] No expired consultant no-show requests found."); return; }

            _logger.LogInformation("[NoShow] Found {Count} expired consultant no-show request(s) to process.", all.Count());

            foreach (var consultation in all)
            {
                var graceHours = consultation.NoShowGraceHours ?? 5;
                var expiresAt = consultation.NoShowRequestedAt!.Value.AddHours(graceHours);
                if (DateTime.UtcNow < expiresAt) continue;

                _logger.LogInformation("[NoShow] Processing consultant no-show for consultation {Id} · Service: {Service}.",
                    consultation.Id, consultation.Service?.ServiceName);

                var escrow = await FindEscrowAsync(consultation.Id);
                if (escrow != null)
                {
                    var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == consultation.CustomerId);
                    if (wallet != null) { wallet.Balance += escrow.Amount; wallet.LastUpdated = DateTime.UtcNow; _walletRepo.Update(wallet); }
                    escrow.Status = "Refunded"; escrow.ResolvedAt = DateTime.UtcNow;
                    _pendingTransactionRepo.Update(escrow);
                    await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = consultation.CustomerId, ConsultantId = null, Amount = escrow.Amount, TransactionType = "ConsultantNoShowRefund", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });
                    _logger.LogInformation("[NoShow] Refunded ${Amount} to customer for consultation {Id}.", escrow.Amount, consultation.Id);
                }

                consultation.Status = "Missed"; consultation.NoShowProcessed = true;
                consultation.Consultant.NoShowCount = (consultation.Consultant.NoShowCount ?? 0) + 1;
                _consultantRepo.Update(consultation.Consultant);
                await _consultationRepo.UpdateAsync(consultation);

                await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId, $"💰 No-show confirmed · ${escrow?.Amount ?? 0} refunded to your wallet · {consultation.Service.ServiceName}", "booking_cancelled");
                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId, $"⚠️ No-show processed for {consultation.Service.ServiceName} · No-show count: {consultation.Consultant.NoShowCount}", "noshow_processed");
                await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, $"❌ No-show grace period expired. ${escrow?.Amount ?? 0} automatically refunded to customer.");
            }

            if (all.Any()) await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[NoShow] Consultant no-show sweep complete.");
        }

        // ── Auto-release expired completion approvals (background sweep) ────────

        public async Task ProcessExpiredApprovalsAsync()
        {
            var cutoff = DateTime.UtcNow.AddHours(-CompletionApprovalGraceHours);
            var all = await _consultationRepo.GetAllAsync(
                c => c.Status == "PendingApproval" && c.CompletionSubmittedAt.HasValue && c.CompletionSubmittedAt.Value <= cutoff,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant).Include(c => c.Service).Include(c => c.ServicePackage));

            if (!all.Any()) { _logger.LogDebug("[ApprovalExpiry] No expired approval windows found."); return; }

            _logger.LogInformation("[ApprovalExpiry] Found {Count} expired approval window(s) to auto-release.", all.Count());

            foreach (var consultation in all)
            {
                var escrow = await FindEscrowAsync(consultation.Id);
                if (escrow == null) { _logger.LogWarning("[ApprovalExpiry] No escrow found for consultation {Id} — skipping.", consultation.Id); continue; }

                var consultantWallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultation.ConsultantId);
                if (consultantWallet == null) { _logger.LogWarning("[ApprovalExpiry] Consultant wallet not found for consultation {Id} — skipping.", consultation.Id); continue; }

                consultantWallet.Balance += escrow.Amount; consultantWallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(consultantWallet);
                escrow.Status = "Released"; escrow.ResolvedAt = DateTime.UtcNow;
                _pendingTransactionRepo.Update(escrow);
                await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = null, ConsultantId = consultation.ConsultantId, Amount = escrow.Amount, TransactionType = "ConsultationPayout", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });

                consultation.Status = "Completed"; consultation.CompletedAt = DateTime.UtcNow;
                await _consultationRepo.UpdateAsync(consultation);

                var amount = ResolvePrice(consultation);
                _logger.LogInformation("[ApprovalExpiry] Auto-released ${Amount} to consultant for consultation {Id} · {Service}.",
                    amount, consultation.Id, consultation.Service?.ServiceName);

                await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, $"⏰ Review window expired · ${amount} automatically released to consultant wallet · {consultation.Service.ServiceName}.");
                await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId, $"⏰ Your {CompletionApprovalGraceHours / 24}-day review window for {consultation.Service.ServiceName} expired · payment was released automatically", "session_complete");
                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId, $"💰 ${escrow.Amount} automatically released to your wallet (review window expired) · {consultation.Service.ServiceName}", "payout_released");
            }

            if (all.Any()) await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[ApprovalExpiry] Approval expiry sweep complete.");
        }

        // ── Auto-resolve expired customer no-show requests (background sweep) ────

        public async Task ProcessExpiredCustomerNoShowsAsync()
        {
            var all = await _consultationRepo.GetAllAsync(
                c => c.CustomerNoShowRequestedAt.HasValue && !c.CustomerNoShowProcessed,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                               .Include(c => c.Service).Include(c => c.ServicePackage));

            if (!all.Any()) { _logger.LogDebug("[CustomerNoShow] No expired customer no-show requests found."); return; }

            _logger.LogInformation("[CustomerNoShow] Found {Count} expired customer no-show request(s) to process.", all.Count());

            foreach (var consultation in all)
            {
                var graceHours = consultation.CustomerNoShowGraceHours ?? 5;
                var expiresAt = consultation.CustomerNoShowRequestedAt!.Value.AddHours(graceHours);
                if (DateTime.UtcNow < expiresAt) continue;

                _logger.LogInformation("[CustomerNoShow] Processing customer no-show for consultation {Id} · Service: {Service}.",
                    consultation.Id, consultation.Service?.ServiceName);

                var escrow = await FindEscrowAsync(consultation.Id);
                var totalAmount = ResolvePrice(consultation);

                consultation.CustomerNoShowReported  = true;
                consultation.CustomerNoShowProcessed = true;
                consultation.Status                  = "Missed";
                consultation.Customer.NoShowCount    = (consultation.Customer.NoShowCount ?? 0) + 1;
                _customerRepo.Update(consultation.Customer);

                if (escrow != null)
                {
                    var consultantPayout = totalAmount * CustomerNoShowPayoutPercentage;
                    var customerRefund = totalAmount - consultantPayout;

                    var cWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId    == consultation.CustomerId);
                    var conWallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultation.ConsultantId);
                    if (cWallet  != null) { cWallet.Balance  += customerRefund; cWallet.LastUpdated  = DateTime.UtcNow; _walletRepo.Update(cWallet); }
                    if (conWallet != null) { conWallet.Balance += consultantPayout; conWallet.LastUpdated = DateTime.UtcNow; _walletRepo.Update(conWallet); }

                    escrow.Status = "PartiallyRefunded"; escrow.ResolvedAt = DateTime.UtcNow;
                    _pendingTransactionRepo.Update(escrow);

                    await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = consultation.CustomerId, ConsultantId = null, Amount = customerRefund, TransactionType = "CustomerNoShowRefund", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });
                    await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = null, ConsultantId = consultation.ConsultantId, Amount = consultantPayout, TransactionType = "CustomerNoShowPayout", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });

                    _logger.LogInformation("[CustomerNoShow] Split ${Total} — ${Payout} to consultant, ${Refund} to customer for consultation {Id}.",
                        totalAmount, consultantPayout, customerRefund, consultation.Id);

                    await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, $"❌ Customer no-show grace period expired. ${consultantPayout} credited to consultant, ${customerRefund} refunded to customer.");
                    await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId, $"💰 Customer no-show confirmed · ${consultantPayout} credited · {consultation.Service.ServiceName}", "payout_released");
                    await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId, $"⚠️ No-show grace period expired · ${customerRefund} refunded · {consultation.Service.ServiceName}", "booking_cancelled");
                }

                await _consultationRepo.UpdateAsync(consultation);
            }

            if (all.Any()) await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[CustomerNoShow] Customer no-show sweep complete.");
        }

        // ── Reschedule ────────────────────────────────────────────────────────

        public async Task RescheduleConsultationAsync(Guid consultationId, DateTime newScheduledAt)
        {
            var userId = GetUserId();
            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                               .Include(c => c.Service).Include(c => c.ServicePackage))
                ?? throw new KeyNotFoundException("Consultation not found.");

            // Only the customer can directly reschedule — consultants use RequestRescheduleAsync
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            if (customer == null || customer.Id != consultation.CustomerId)
                throw new UnauthorizedAccessException(
                    "Only the customer can reschedule a session. As a consultant, please use 'Request reschedule' instead.");

            if (consultation.Status is "Completed" or "Rejected" or "Cancelled" or "Missed" or "PendingApproval")
                throw new InvalidOperationException("Cannot reschedule a completed, closed, or pending-approval consultation.");

            if (newScheduledAt <= DateTime.UtcNow)
                throw new InvalidOperationException("New schedule must be in the future.");

            var oldDate = consultation.ScheduledAt;
            consultation.ScheduledAt             = newScheduledAt;
            consultation.EndAt                   = newScheduledAt.AddMinutes(consultation.ServicePackage?.DurationMinutes ?? 60);
            consultation.NoShowRequestedAt       = null;
            consultation.NoShowGraceHours        = null;
            consultation.NoShowProcessed         = false;
            consultation.RescheduleRequestedAt   = null;
            consultation.RescheduleRequestReason = null;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"📅 Session rescheduled from {oldDate:MMM d, h:mm tt} → {newScheduledAt:MMM d, h:mm tt} UTC.");

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"📅 Session rescheduled to {newScheduledAt:MMM d, h:mm tt} · {consultation.Service.ServiceName}", "rescheduled");
            await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                $"📅 Customer rescheduled the session to {newScheduledAt:MMM d, h:mm tt} · {consultation.Service.ServiceName}", "rescheduled");

            _logger.LogInformation("[Reschedule] Consultation {Id} rescheduled from {Old} to {New} UTC.",
                consultationId, oldDate, newScheduledAt);
        }

        // ── No-show legacy (immediate, customer-reported) ─────────────────────

        public async Task<ConsultationResponse> ReportConsultantNoShowAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                           ?? throw new UnauthorizedAccessException("Customer not found.");
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (consultation.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("Not authorized.");

            var gracePeriodEnd = consultation.ScheduledAt.AddMinutes(GracePeriodMinutes);
            if (DateTime.UtcNow < gracePeriodEnd)
                throw new InvalidOperationException($"Please wait until the grace period ends.");

            if (consultation.Status is not ("Approved" or "InProgress" or "OverdueReview"))
                throw new InvalidOperationException("Cannot report no-show for this status.");

            var escrow = await FindEscrowAsync(consultationId);
            consultation.ConsultantNoShowReported = true;
            consultation.Status                   = "Missed";
            consultation.NoShowProcessed          = true;
            consultation.Consultant.NoShowCount   = (consultation.Consultant.NoShowCount ?? 0) + 1;
            _consultantRepo.Update(consultation.Consultant);

            if (escrow != null)
            {
                var w = await _walletRepo.GetSingleByAsync(w => w.CustomerId == consultation.CustomerId)
                        ?? throw new InvalidOperationException("Wallet not found.");
                w.Balance += escrow.Amount; w.LastUpdated = DateTime.UtcNow; _walletRepo.Update(w);
                escrow.Status = "Refunded"; escrow.ResolvedAt = DateTime.UtcNow; _pendingTransactionRepo.Update(escrow);
                await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = consultation.CustomerId, ConsultantId = null, Amount = escrow.Amount, TransactionType = "ConsultantNoShowRefund", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });
            }

            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, $"❌ Consultant no-show confirmed. ${escrow?.Amount ?? 0} refunded.");
            await _sendbirdService.SendNotificationAsync(customer.UserId, $"💰 No-show confirmed · ${escrow?.Amount ?? 0} refunded", "booking_cancelled");

            _logger.LogInformation("[ReportNoShow] Consultant no-show confirmed for consultation {Id} · Refunded ${Amount}.",
                consultationId, escrow?.Amount ?? 0);

            var response = _mapper.Map<ConsultationResponse>(consultation);
            response.PendingAmount = 0;
            response.Price = ResolvePrice(consultation);
            return response;
        }

        // ── Customer no-show ──────────────────────────────────────────────────

        public async Task<ConsultationResponse> ReportCustomerNoShowAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");
            var consultation = await _consultationRepo.GetSingleByAsync(c => c.Id == consultationId,
                include: q => q.Include(c => c.Service).Include(c => c.ServicePackage)
                               .Include(c => c.Consultant).Include(c => c.Customer))
                ?? throw new KeyNotFoundException("Consultation not found.");

            await EnsureConsultantOwnershipAsync(consultation, userId);

            var gracePeriodEnd = consultation.ScheduledAt.AddMinutes(GracePeriodMinutes);
            if (DateTime.UtcNow < gracePeriodEnd)
                throw new InvalidOperationException("Please wait until the grace period ends.");

            if (consultation.Status is not ("Approved" or "InProgress"))
                throw new InvalidOperationException("Cannot report no-show for this status.");

            var escrow = await FindEscrowAsync(consultationId);
            consultation.CustomerNoShowReported = true;
            consultation.Status                 = "Missed";
            consultation.Customer.NoShowCount   = (consultation.Customer.NoShowCount ?? 0) + 1;
            _customerRepo.Update(consultation.Customer);

            var totalAmount = ResolvePrice(consultation);

            if (escrow != null)
            {
                var consultantPayout = totalAmount * CustomerNoShowPayoutPercentage;
                var customerRefund = totalAmount - consultantPayout;

                var cWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId    == consultation.CustomerId)  ?? throw new InvalidOperationException("Customer wallet not found.");
                var conWallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultation.ConsultantId) ?? throw new InvalidOperationException("Consultant wallet not found.");
                cWallet.Balance  += customerRefund; cWallet.LastUpdated  = DateTime.UtcNow; _walletRepo.Update(cWallet);
                conWallet.Balance += consultantPayout; conWallet.LastUpdated = DateTime.UtcNow; _walletRepo.Update(conWallet);

                escrow.Status = "PartiallyRefunded"; escrow.ResolvedAt = DateTime.UtcNow; _pendingTransactionRepo.Update(escrow);

                await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = consultation.CustomerId, ConsultantId = null, Amount = customerRefund, TransactionType = "CustomerNoShowRefund", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });
                await _walletTransactionRepo.AddAsync(new WalletTransaction { CustomerId = null, ConsultantId = consultation.ConsultantId, Amount = consultantPayout, TransactionType = "CustomerNoShowPayout", Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });

                await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl, $"❌ Customer no-show. ${consultantPayout} credited to consultant, ${customerRefund} refunded to customer.");
                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId, $"💰 Customer no-show · ${consultantPayout} credited to your wallet", "payout_released");

                _logger.LogInformation("[CustomerNoShow] Consultation {Id} · ${Payout} to consultant, ${Refund} to customer.",
                    consultationId, consultantPayout, customerRefund);
            }

            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            var response = _mapper.Map<ConsultationResponse>(consultation);
            response.PendingAmount = 0;
            response.Price = totalAmount;
            return response;
        }

        // ── Request customer no-show (consultant side) ───────────────────────

        public async Task RequestCustomerNoShowAsync(Guid consultationId, int graceHours)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId && c.ConsultantId == consultant.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Service))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (!new[] { "Approved", "InProgress", "OverdueReview" }.Contains(consultation.Status))
                throw new InvalidOperationException("No-show can only be requested for active sessions.");

            if (consultation.CustomerNoShowRequestedAt.HasValue && !consultation.CustomerNoShowProcessed)
                throw new InvalidOperationException("A customer no-show request is already pending.");

            var gracePeriodEnd = consultation.ScheduledAt.AddMinutes(GracePeriodMinutes);
            if (DateTime.UtcNow < gracePeriodEnd)
                throw new InvalidOperationException("Please wait until the grace period ends.");

            graceHours = Math.Clamp(graceHours, 1, 48);

            consultation.CustomerNoShowRequestedAt = DateTime.UtcNow;
            consultation.CustomerNoShowGraceHours  = graceHours;
            consultation.CustomerNoShowProcessed   = false;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"⚠️ Consultant reported you as no-show · {consultation.Service.ServiceName} · Respond within {graceHours}h", "customer_noshow_request");
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"⚠️ Consultant has reported a customer no-show. Customer has {graceHours} hours to respond before 50/50 split is applied.");

            _logger.LogInformation("[CustomerNoShowRequest] Consultant reported customer no-show for consultation {Id} · Grace: {Hours}h.", consultationId, graceHours);
        }

        public async Task<int> GetActiveBookingsCountAsync(int consultantId)
        {
            var activeStatuses = new[] { "Pending", "Approved", "InProgress", "OverdueReview" };
            var count = await _consultationRepo.CountAsync(c =>
                c.ConsultantId == consultantId && activeStatuses.Contains(c.Status));

            _logger.LogDebug("[ActiveBookings] Consultant {ConsultantId} has {Count} active booking(s).", consultantId, count);

            return (int)count;   // ← explicit cast from long to int
        }



        // ── Dismiss customer no-show (customer says "I'm joining") ────────────

        public async Task DismissCustomerNoShowRequestAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId && c.CustomerId == customer.Id,
                include: q => q.Include(c => c.Consultant).Include(c => c.Service))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (!consultation.CustomerNoShowRequestedAt.HasValue)
                throw new InvalidOperationException("No pending customer no-show request.");

            consultation.CustomerNoShowRequestedAt = null;
            consultation.CustomerNoShowGraceHours  = null;
            consultation.CustomerNoShowProcessed   = false;
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                $"✅ Customer confirmed they are joining · {consultation.Service.ServiceName}", "customer_noshow_dismissed");
            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"✅ Customer confirmed presence. No-show request dismissed. Session continues.");

            _logger.LogInformation("[DismissCustomerNoShow] Customer dismissed no-show for consultation {Id}.", consultationId);
        }

        // ── Process expired customer no-shows (called from frontend) ─────────

        public async Task ProcessExpiredCustomerNoShowForConsultationAsync(Guid consultationId)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException();

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId && c.ConsultantId == consultant.Id &&
                     c.CustomerNoShowRequestedAt.HasValue && !c.CustomerNoShowProcessed,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant).Include(c => c.Service))
                ?? throw new KeyNotFoundException("No pending customer no-show request found.");

            var expiresAt = consultation.CustomerNoShowRequestedAt!.Value.AddHours(consultation.CustomerNoShowGraceHours ?? 5);
            if (DateTime.UtcNow < expiresAt)
                throw new InvalidOperationException("Grace period has not expired yet.");

            consultation.CustomerNoShowProcessed = true;
            await ReportCustomerNoShowAsync(consultationId);
        }

        // ── Read ──────────────────────────────────────────────────────────────

        public async Task<IEnumerable<ConsultationResponse>> GetMyConsultationsAsync()
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                           ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultations = (await _consultationRepo.GetAllAsync(c => c.CustomerId == customer.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                               .Include(c => c.Service).Include(c => c.ServicePackage))).ToList();

            var responses = _mapper.Map<IEnumerable<ConsultationResponse>>(consultations).ToList();
            for (int i = 0; i < responses.Count; i++)
            {
                var r = responses[i];
                var c = consultations[i];
                var escrow = await FindEscrowAsync(r.Id);
                r.PendingAmount = escrow?.Amount ?? 0;
                r.Price = ResolvePrice(c);
            }
            return responses;
        }

        public async Task RequestRescheduleAsync(Guid consultationId, string reason)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == consultationId && c.ConsultantId == consultant.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Service))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (!new[] { "Pending", "Approved" }.Contains(consultation.Status))
                throw new InvalidOperationException("Reschedule requests can only be made for pending or approved consultations.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Please provide a reason for requesting a reschedule.");

            if (consultation.RescheduleRequestedAt.HasValue)
                throw new InvalidOperationException("A reschedule request is already pending. The customer has been notified.");

            consultation.RescheduleRequestedAt   = DateTime.UtcNow;
            consultation.RescheduleRequestReason = reason.Trim();
            await _consultationRepo.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();

            await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                $"📅 {consultant.FirstName} {consultant.LastName} has requested to reschedule this session.\n" +
                $"Reason: \"{reason.Trim()}\"\nPlease go to your Consultations page and choose a new time.");
            await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                $"📅 {consultant.FirstName} {consultant.LastName} requested to reschedule your {consultation.Service.ServiceName} session — please pick a new time",
                "reschedule_request");

            _logger.LogInformation("[RequestReschedule] Consultant requested reschedule for consultation {Id} · Reason: {Reason}.",
                consultationId, reason);
        }

        public async Task<IEnumerable<ConsultationResponse>> GetConsultantConsultationsAsync()
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");

            var consultations = (await _consultationRepo.GetAllAsync(c => c.ConsultantId == consultant.Id,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                               .Include(c => c.Service).Include(c => c.ServicePackage))).ToList();

            var responses = _mapper.Map<IEnumerable<ConsultationResponse>>(consultations).ToList();

            var customerIds = consultations.Select(c => c.CustomerId).Distinct().ToList();
            var allCustomerConsultations = await _consultationRepo.GetByAsync(c => customerIds.Contains(c.CustomerId));
            var allCustomerReviews = await _reviewRepo.GetByAsync(r => customerIds.Contains(r.CustomerId));

            var bookingsMap = allCustomerConsultations.GroupBy(c => c.CustomerId).ToDictionary(g => g.Key, g => g.Count());
            var completedMap = allCustomerConsultations.Where(c => c.Status == "Completed").GroupBy(c => c.CustomerId).ToDictionary(g => g.Key, g => g.Count());
            var reviewMap = allCustomerReviews.GroupBy(r => r.CustomerId).ToDictionary(g => g.Key, g => (Count: g.Count(), Avg: g.Average(r => (double)r.Rating)));

            for (int i = 0; i < responses.Count; i++)
            {
                var r = responses[i];
                var c = consultations[i];
                var escrow = await FindEscrowAsync(r.Id);
                r.PendingAmount = escrow?.Amount ?? 0;
                r.Price = ResolvePrice(c);
                r.CustomerTotalBookings     = bookingsMap.TryGetValue(c.CustomerId, out var tb) ? tb : 0;
                r.CustomerCompletedBookings = completedMap.TryGetValue(c.CustomerId, out var cb) ? cb : 0;
                if (reviewMap.TryGetValue(c.CustomerId, out var rv)) { r.CustomerReviewCount = rv.Count; r.CustomerAverageRating = Math.Round(rv.Avg, 1); }
                else { r.CustomerReviewCount = 0; r.CustomerAverageRating = 0; }
            }
            return responses;
        }

        // ── Flag overdue InProgress sessions (background sweep) ──────────────
        // After StartedAt + durationMinutes + 2h grace, status → OverdueReview.

        public async Task ProcessOverdueInProgressSessionsAsync()
        {
            var all = await _consultationRepo.GetAllAsync(
                c => c.Status == "InProgress" && c.StartedAt.HasValue,
                include: q => q.Include(c => c.Customer).Include(c => c.Consultant)
                               .Include(c => c.Service).Include(c => c.ServicePackage));

            if (!all.Any()) { _logger.LogDebug("[OverdueReview] No InProgress sessions found."); return; }

            _logger.LogDebug("[OverdueReview] Checking {Count} InProgress session(s) for overdue status.", all.Count());

            var flagged = 0;

            foreach (var consultation in all)
            {
                var durationMinutes = consultation.ServicePackage?.DurationMinutes ?? 60;
                var overdueAt = consultation.StartedAt!.Value.AddMinutes(durationMinutes).AddHours(2);
                if (DateTime.UtcNow < overdueAt) continue;

                _logger.LogWarning("[OverdueReview] Flagging consultation {Id} as OverdueReview · StartedAt: {StartedAt} · Duration: {Duration}min · Overdue since: {OverdueSince}.",
                    consultation.Id, consultation.StartedAt, durationMinutes, overdueAt);

                consultation.Status = "OverdueReview";
                await _consultationRepo.UpdateAsync(consultation);
                flagged++;

                await _sendbirdService.SendAdminMessageAsync(consultation.SendbirdChannelUrl,
                    $"⏰ This session exceeded its {durationMinutes}-minute duration and has not been submitted for review. " +
                    $"{consultation.Consultant.FirstName}, please submit your work summary to release the ${ResolvePrice(consultation)} in escrow. " +
                    $"If the session was not completed, the customer may report a no-show.");

                await _sendbirdService.SendNotificationAsync(consultation.Consultant.UserId,
                    $"⏰ Your session for {consultation.Service.ServiceName} is overdue — please submit your work summary", "session_overdue");
                await _sendbirdService.SendNotificationAsync(consultation.Customer.UserId,
                    $"⏰ Your {consultation.Service.ServiceName} session has exceeded its duration. The consultant has been prompted to submit — you may also report a no-show if they did not attend.", "session_overdue");
            }

            if (flagged > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("[OverdueReview] Flagged {Count} session(s) as OverdueReview.", flagged);
            }
            else
            {
                _logger.LogDebug("[OverdueReview] No sessions overdue at this time.");
            }
        }
    }
}