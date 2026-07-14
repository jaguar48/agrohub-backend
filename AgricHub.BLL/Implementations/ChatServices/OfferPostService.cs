using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.BLL.Interfaces.IChatServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AgricHub.BLL.Implementations.ChatServices
{
    public class OfferPostService : IOfferPostService
    {
        private readonly IRepository<OfferPost> _postRepo;
        private readonly IRepository<CustomOffer> _pitchRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Service> _serviceRepo;
        private readonly IRepository<Business> _businessRepo;
        private readonly IRepository<Category> _categoryRepo;
        private readonly IRepository<ChatSession> _chatSessionRepo;
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<PendingTransaction> _pendingTxRepo;
        private readonly IRepository<WalletTransaction> _walletTxRepo;
        private readonly IRepository<Review> _reviewRepo;
        private readonly ISendbirdService _sendbirdService;
        private readonly IStorageService _storage;
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPlatformSettingsService _settings;
        private readonly IEmailService _emailService;

        public OfferPostService(
            IUnitOfWork uow,
            ISendbirdService sendbirdService,
            IStorageService storage,
            IHttpContextAccessor httpContextAccessor,
            IPlatformSettingsService settings,
            IEmailService emailService)
        {
            _uow                 = uow;
            _postRepo            = uow.GetRepository<OfferPost>();
            _pitchRepo           = uow.GetRepository<CustomOffer>();
            _customerRepo        = uow.GetRepository<Customer>();
            _consultantRepo      = uow.GetRepository<Consultant>();
            _serviceRepo         = uow.GetRepository<Service>();
            _businessRepo        = uow.GetRepository<Business>();
            _categoryRepo        = uow.GetRepository<Category>();
            _chatSessionRepo     = uow.GetRepository<ChatSession>();
            _walletRepo          = uow.GetRepository<Wallet>();
            _consultationRepo    = uow.GetRepository<Consultation>();
            _pendingTxRepo       = uow.GetRepository<PendingTransaction>();
            _walletTxRepo        = uow.GetRepository<WalletTransaction>();
            _reviewRepo          = uow.GetRepository<Review>();
            _sendbirdService     = sendbirdService;
            _storage             = storage;
            _httpContextAccessor = httpContextAccessor;
            _settings            = settings;
            _emailService        = emailService;
        }

        private string GetUserId() =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        // ── Create post (customer) ────────────────────────────────────────────

        public async Task<OfferPostResponse> CreatePostAsync(CreateOfferPostRequest request)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new InvalidOperationException("Please give your request a short title.");
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new InvalidOperationException("Please describe what you need.");
            if (request.PreferredAt.HasValue && request.PreferredAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Preferred date must be in the future.");

            var post = new OfferPost
            {
                Id          = Guid.NewGuid(),
                CustomerId  = customer.Id,
                CategoryId  = request.CategoryId,
                Title       = request.Title.Trim(),
                Description = request.Description.Trim(),
                Budget      = request.Budget,
                PreferredAt = request.PreferredAt,
                Status      = "Open",
                CreatedAt   = DateTime.UtcNow,
            };
            await _postRepo.AddAsync(post);
            await _uow.SaveChangesAsync();

            // Notify relevant consultants that a new request just went up — same
            // idea as a Fiverr seller getting pinged about a new matching buyer
            // request. Without this, consultants only find posts by manually
            // checking "Browse requests", which most won't do proactively.
            await NotifyConsultantsOfNewPostAsync(post);

            return await ToResponseAsync(post, customer);
        }

        /// <summary>
        /// Notifies consultants about a new open post. If the post has a category,
        /// only consultants who have at least one service in that category are
        /// notified (relevant-only, avoids spamming everyone). If no category was
        /// chosen, all verified consultants are notified since the request could
        /// suit anyone.
        /// </summary>
        private async Task NotifyConsultantsOfNewPostAsync(OfferPost post)
        {
            try
            {
                List<Consultant> targets;

                if (post.CategoryId.HasValue)
                {
                    var servicesInCategory = await _serviceRepo.GetAllAsync(
                        s => s.CategoryId == post.CategoryId.Value,
                        include: q => q.Include(s => s.Business));

                    var consultantIds = servicesInCategory
                        .Select(s => s.Business.ConsultantId)
                        .Distinct()
                        .ToList();

                    targets = new List<Consultant>();
                    foreach (var id in consultantIds)
                    {
                        var c = await _consultantRepo.GetSingleByAsync(c => c.Id == id);
                        if (c != null) targets.Add(c);
                    }
                }
                else
                {
                    var all = await _consultantRepo.GetAllAsync(c => c.IsVerified);
                    targets = all.ToList();
                }

                var budgetText = post.Budget.HasValue ? $" · Budget: ₦{post.Budget:N2}" : "";
                foreach (var consultant in targets)
                {
                    if (string.IsNullOrWhiteSpace(consultant.UserId)) continue;
                    try
                    {
                        await _sendbirdService.SendNotificationAsync(consultant.UserId,
                            $"📋 New request: \"{post.Title}\"{budgetText}",
                            "offer_post_created");
                    }
                    catch { /* one failed notification shouldn't block the rest */ }
                }
            }
            catch (Exception)
            {
                // Never let notification fan-out break post creation itself
            }
        }

        // ── Browse open posts (consultant feed) ───────────────────────────────

        public async Task<IEnumerable<OfferPostResponse>> GetOpenPostsAsync(int? categoryId = null)
        {
            var posts = await _postRepo.GetAllAsync(
                p => p.Status == "Open" && (categoryId == null || p.CategoryId == categoryId),
                orderBy: q => q.OrderByDescending(p => p.CreatedAt),
                include: q => q.Include(p => p.Customer).Include(p => p.Category));

            var result = new List<OfferPostResponse>();
            foreach (var p in posts)
                result.Add(await ToResponseAsync(p, p.Customer));
            return result;
        }

        public async Task<IEnumerable<OfferPostResponse>> GetMyPostsAsync()
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var posts = await _postRepo.GetAllAsync(
                p => p.CustomerId == customer.Id,
                orderBy: q => q.OrderByDescending(p => p.CreatedAt),
                include: q => q.Include(p => p.Category));

            var result = new List<OfferPostResponse>();
            foreach (var p in posts)
                result.Add(await ToResponseAsync(p, customer));
            return result;
        }

        public async Task<OfferPostDetailResponse> GetPostDetailAsync(Guid postId)
        {
            var post = await _postRepo.GetSingleByAsync(p => p.Id == postId,
                include: q => q.Include(p => p.Customer).Include(p => p.Category))
                ?? throw new KeyNotFoundException("Request not found.");

            var pitches = await _pitchRepo.GetAllAsync(
                c => c.OfferPostId == postId,
                orderBy: q => q.OrderByDescending(c => c.CreatedAt),
                include: q => q.Include(c => c.Service));

            var basic = await ToResponseAsync(post, post.Customer);
            var detail = new OfferPostDetailResponse
            {
                Id = basic.Id,
                CustomerId = basic.CustomerId,
                CustomerName = basic.CustomerName,
                CategoryId = basic.CategoryId,
                CategoryName = basic.CategoryName,
                Title = basic.Title,
                Description = basic.Description,
                Budget = basic.Budget,
                PreferredAt = basic.PreferredAt,
                Status = basic.Status,
                CreatedAt = basic.CreatedAt,
                PitchCount = basic.PitchCount,
            };

            foreach (var pitch in pitches)
                detail.Pitches.Add(await ToPitchResponseAsync(pitch));

            return detail;
        }

        public async Task ClosePostAsync(Guid postId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var post = await _postRepo.GetSingleByAsync(p => p.Id == postId)
                ?? throw new KeyNotFoundException("Request not found.");
            if (post.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You are not authorized to close this request.");

            post.Status   = "Closed";
            post.ClosedAt = DateTime.UtcNow;
            await _postRepo.UpdateAsync(post);
            await _uow.SaveChangesAsync();
        }

        // ── Submit a pitch (consultant) ────────────────────────────────────────

        public async Task<PitchResponse> SubmitPitchAsync(SubmitPitchRequest request, IFormFile? portfolioFile)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            // ── Verified-consultant gate — same setting BookConsultationAsync
            // checks, applied consistently so unverified consultants can't take
            // work through either booking path. ──
            var requiresVerificationRaw = await _settings.GetAsync("booking.requiresVerification");
            var requiresVerification = requiresVerificationRaw == null || requiresVerificationRaw == "true";
            if (requiresVerification && !consultant.IsVerified)
                throw new InvalidOperationException("You must complete verification before pitching on requests.");

            var post = await _postRepo.GetSingleByAsync(p => p.Id == request.OfferPostId,
                include: q => q.Include(p => p.Customer))
                ?? throw new KeyNotFoundException("Request not found.");
            if (post.Status != "Open")
                throw new InvalidOperationException("This request is no longer accepting pitches.");

            var service = await _serviceRepo.GetSingleByAsync(s => s.Id == request.ServiceId,
                include: q => q.Include(s => s.Business))
                ?? throw new KeyNotFoundException("Service not found.");
            var business = await _businessRepo.GetSingleByAsync(
                b => b.Id == service.BusinessId && b.ConsultantId == consultant.Id)
                ?? throw new UnauthorizedAccessException("This service does not belong to you.");

            if (request.ScheduledAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Proposed time must be in the future.");
            if (request.DurationMinutes <= 0)
                throw new InvalidOperationException("Please specify a valid duration.");
            if (string.IsNullOrWhiteSpace(request.PitchMessage))
                throw new InvalidOperationException("Please include a short pitch message.");

            // One pitch per consultant per post
            var already = await _pitchRepo.AnyAsync(c =>
                c.OfferPostId == request.OfferPostId && c.ConsultantId == consultant.Id);
            if (already)
                throw new InvalidOperationException("You've already submitted a pitch for this request.");

            // Ensure a chat session exists between this customer and consultant —
            // same pattern as ChatService.InitiateChatAsync — so the pitch can be
            // shown as a card in their conversation, Fiverr-style.
            var chatSession = await _chatSessionRepo.GetSingleByAsync(cs =>
                cs.CustomerId == post.CustomerId && cs.ConsultantId == consultant.Id);

            string channelUrl;
            Guid chatSessionId;
            if (chatSession != null)
            {
                channelUrl = chatSession.SendbirdChannelUrl;
                chatSessionId = chatSession.Id;
            }
            else
            {
                await _sendbirdService.EnsureSendbirdUserAsync(post.Customer.UserId, $"{post.Customer.FirstName} {post.Customer.LastName}");
                await _sendbirdService.EnsureSendbirdUserAsync(consultant.UserId, $"{consultant.FirstName} {consultant.LastName}");
                channelUrl = await _sendbirdService.CreateGroupChannelAsync(post.Customer.UserId, consultant.UserId);

                var newSession = new ChatSession
                {
                    Id                 = Guid.NewGuid(),
                    CustomerId         = post.CustomerId,
                    ConsultantId       = consultant.Id,
                    ServiceId          = service.Id,
                    SendbirdChannelUrl = channelUrl,
                    CreatedAt          = DateTime.UtcNow,
                };
                await _chatSessionRepo.AddAsync(newSession);
                await _uow.SaveChangesAsync();
                chatSessionId = newSession.Id;
            }

            string? portfolioUrl = null, portfolioFileName = null;
            if (portfolioFile != null && portfolioFile.Length > 0)
            {
                await using var stream = portfolioFile.OpenReadStream();
                portfolioUrl = await _storage.UploadAsync(stream, portfolioFile.FileName, "agrichub/portfolio");
                portfolioFileName = portfolioFile.FileName;
            }

            var pitch = new CustomOffer
            {
                Id                  = Guid.NewGuid(),
                ChatSessionId       = chatSessionId,
                OfferPostId         = post.Id,
                ConsultantId        = consultant.Id,
                ServiceId           = service.Id,
                Price               = request.Price,
                Description         = request.Description,
                PitchMessage        = request.PitchMessage,
                PortfolioUrl        = portfolioUrl,
                PortfolioFileName   = portfolioFileName,
                IncludesOnsiteVisit = request.IncludesOnsiteVisit,
                ScheduledAt         = request.ScheduledAt,
                DurationMinutes     = request.DurationMinutes,
                Status              = "Pending",
                CreatedAt           = DateTime.UtcNow,
            };
            await _pitchRepo.AddAsync(pitch);
            await _uow.SaveChangesAsync();

            // Chat card — same "custom offer" render path the frontend already
            // uses (isOfferMessage() looks for an OfferId in the message data).
            try
            {
                var cardMsg =
                    $"💼 {consultant.FirstName} {consultant.LastName} pitched on \"{post.Title}\": " +
                    $"{service.ServiceName} · ₦{pitch.Price:N2}. {pitch.PitchMessage}";
                var cardData = new
                {
                    OfferId = pitch.Id,
                    OfferPostId = post.Id,
                    OfferPostTitle = post.Title,
                    ServiceId = service.Id,
                    ServiceName = service.ServiceName,
                    // Pitch cards go through SendAdminMessageAsync (SenderId is null,
                    // sender shows as "system" in the DTO), so the frontend has no
                    // way to know which consultant this is from the message itself.
                    // Embed it explicitly so the "view profile" link works.
                    ConsultantId = consultant.Id,
                    ConsultantName = $"{consultant.FirstName} {consultant.LastName}",
                    pitch.Price,
                    pitch.Description,
                    PitchMessage = pitch.PitchMessage,
                    PortfolioUrl = pitch.PortfolioUrl,
                    PortfolioFileName = pitch.PortfolioFileName,
                    pitch.IncludesOnsiteVisit,
                    pitch.ScheduledAt,
                    pitch.DurationMinutes,
                };
                await _sendbirdService.SendAdminMessageAsync(channelUrl, cardMsg, cardData);
            }
            catch { /* chat card failure shouldn't block the pitch itself */ }

            try
            {
                await _sendbirdService.SendNotificationAsync(post.Customer.UserId,
                    $"💼 New pitch on \"{post.Title}\" from {consultant.FirstName} {consultant.LastName} · ₦{pitch.Price:N2}",
                    "pitch_received");
            }
            catch { }

            try
            {
                await _emailService.SendGenericNotificationAsync(
                    post.Customer.Email, $"{post.Customer.FirstName} {post.Customer.LastName}",
                    $"New pitch on \"{post.Title}\"",
                    "You've received a new pitch",
                    $"<p>{consultant.FirstName} {consultant.LastName} pitched on your request \"<strong>{post.Title}</strong>\":</p>" +
                    $"<p>{pitch.PitchMessage}</p>" +
                    $"<p><strong>Price:</strong> ₦{pitch.Price:N2}</p>",
                    "View pitch", "https://agrichub.io/customer/requests");
            }
            catch { }

            return await ToPitchResponseAsync(pitch);
        }

        public async Task<IEnumerable<PitchResponse>> GetMyPitchesAsync()
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            var pitches = await _pitchRepo.GetAllAsync(
                c => c.ConsultantId == consultant.Id && c.OfferPostId != null,
                orderBy: q => q.OrderByDescending(c => c.CreatedAt),
                include: q => q.Include(c => c.Service));

            var result = new List<PitchResponse>();
            foreach (var p in pitches)
                result.Add(await ToPitchResponseAsync(p));
            return result;
        }

        // ── Accept / reject a pitch (customer) ─────────────────────────────────

        public async Task<PitchResponse> AcceptPitchAsync(Guid pitchId)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var pitch = await _pitchRepo.GetSingleByAsync(c => c.Id == pitchId,
                include: q => q.Include(c => c.Service).Include(c => c.OfferPost))
                ?? throw new KeyNotFoundException("Pitch not found.");

            var post = pitch.OfferPost
                ?? throw new InvalidOperationException("This pitch is not linked to a request.");
            if (post.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You are not authorized to accept this pitch.");
            if (pitch.Status != "Pending")
                throw new InvalidOperationException("Only pending pitches can be accepted.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.Id == pitch.ConsultantId)
                ?? throw new KeyNotFoundException("Consultant not found.");

            var isSlotTaken = await _consultationRepo.AnyAsync(c =>
                c.ConsultantId == pitch.ConsultantId && c.ScheduledAt == pitch.ScheduledAt!.Value);
            if (isSlotTaken)
                throw new InvalidOperationException("That time slot is already booked for this consultant.");

            var customerWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id);
            if (customerWallet == null || customerWallet.Balance < pitch.Price)
                throw new InvalidOperationException("Insufficient wallet balance. Please top up your wallet.");

            await _uow.BeginTransactionAsync();
            try
            {
                customerWallet.Balance    -= pitch.Price;
                customerWallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(customerWallet);

                var consultation = new Consultation
                {
                    Id                    = Guid.NewGuid(),
                    CustomerId            = customer.Id,
                    ConsultantId          = pitch.ConsultantId,
                    ServiceId             = pitch.ServiceId,
                    ServicePackageId      = null,
                    ScheduledAt           = pitch.ScheduledAt!.Value,
                    EndAt                 = pitch.ScheduledAt!.Value.AddMinutes(pitch.DurationMinutes),
                    Status                = "Pending",
                    SendbirdChannelUrl    = (await _chatSessionRepo.GetSingleByAsync(cs => cs.Id == pitch.ChatSessionId))?.SendbirdChannelUrl ?? "",
                    CreatedAt             = DateTime.UtcNow,
                    IsCustomOffer         = true,
                    OfferPostId           = post.Id,
                    CustomPrice           = pitch.Price,
                    CustomDurationMinutes = pitch.DurationMinutes,
                };
                await _consultationRepo.AddAsync(consultation);

                await _pendingTxRepo.AddAsync(new PendingTransaction
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    ConsultationId = consultation.Id,
                    Amount = pitch.Price,
                    Status = "Held",
                    CreatedAt = DateTime.UtcNow,
                });
                await _walletTxRepo.AddAsync(new WalletTransaction
                {
                    CustomerId = customer.Id,
                    ConsultantId = null,
                    Amount = -pitch.Price,
                    TransactionType = "CustomOfferPayment",
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                });

                pitch.Status     = "Accepted";
                pitch.AcceptedAt = DateTime.UtcNow;
                await _pitchRepo.UpdateAsync(pitch);

                post.Status   = "Closed";
                post.ClosedAt = DateTime.UtcNow;
                await _postRepo.UpdateAsync(post);

                // Reject all other pending pitches on this post — no charge was ever
                // taken from those consultants, so nothing to refund.
                var others = await _pitchRepo.GetAllAsync(c =>
                    c.OfferPostId == post.Id && c.Id != pitch.Id && c.Status == "Pending");
                foreach (var other in others)
                {
                    other.Status = "Rejected";
                    await _pitchRepo.UpdateAsync(other);
                }

                await _uow.SaveChangesAsync();
                await _uow.CommitTransactionAsync();

                var channelUrl = (await _chatSessionRepo.GetSingleByAsync(cs => cs.Id == pitch.ChatSessionId))?.SendbirdChannelUrl;
                if (!string.IsNullOrEmpty(channelUrl))
                {
                    try
                    {
                        await _sendbirdService.SendAdminMessageAsync(channelUrl,
                            $"✅ Pitch accepted for \"{post.Title}\" · {pitch.Service?.ServiceName}. " +
                            $"₦{pitch.Price:N2} held in escrow. Scheduled: {pitch.ScheduledAt:yyyy-MM-dd HH:mm}.");
                    }
                    catch { }
                }

                try
                {
                    await _sendbirdService.SendNotificationAsync(consultant.UserId,
                        $"✅ {customer.FirstName} {customer.LastName} accepted your pitch on \"{post.Title}\" · ₦{pitch.Price:N2} held in escrow",
                        "pitch_accepted");
                }
                catch { }

                try
                {
                    await _emailService.SendGenericNotificationAsync(
                        consultant.Email, $"{consultant.FirstName} {consultant.LastName}",
                        $"Pitch accepted — \"{post.Title}\"",
                        "Your pitch was accepted!",
                        $"<p>{customer.FirstName} {customer.LastName} accepted your pitch on \"<strong>{post.Title}</strong>\".</p>" +
                        $"<p>₦{pitch.Price:N2} is now held in escrow. Scheduled: {pitch.ScheduledAt:MMM d, h:mm tt}.</p>",
                        "View schedule", "https://agrichub.io/consultant/schedule");
                }
                catch { }

                foreach (var other in others)
                {
                    try
                    {
                        var otherConsultant = await _consultantRepo.GetSingleByAsync(c => c.Id == other.ConsultantId);
                        if (otherConsultant != null)
                        {
                            await _sendbirdService.SendNotificationAsync(otherConsultant.UserId,
                                $"Your pitch on \"{post.Title}\" was not selected this time.",
                                "pitch_rejected");
                            await _emailService.SendGenericNotificationAsync(
                                otherConsultant.Email, $"{otherConsultant.FirstName} {otherConsultant.LastName}",
                                $"Pitch update — \"{post.Title}\"",
                                "Your pitch wasn't selected this time",
                                $"<p>The customer chose another pitch for \"<strong>{post.Title}</strong>\". No charge was made — keep an eye out for new requests.</p>",
                                "Browse requests", "https://agrichub.io/consultant/requests");
                        }
                    }
                    catch { }
                }

                return await ToPitchResponseAsync(pitch);
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<PitchResponse> RejectPitchAsync(Guid pitchId, string reason)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var pitch = await _pitchRepo.GetSingleByAsync(c => c.Id == pitchId,
                include: q => q.Include(c => c.Service).Include(c => c.OfferPost))
                ?? throw new KeyNotFoundException("Pitch not found.");

            var post = pitch.OfferPost ?? throw new InvalidOperationException("This pitch is not linked to a request.");
            if (post.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You are not authorized to reject this pitch.");
            if (pitch.Status != "Pending")
                throw new InvalidOperationException("Only pending pitches can be rejected.");

            pitch.Status = "Rejected";
            await _pitchRepo.UpdateAsync(pitch);
            await _uow.SaveChangesAsync();

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.Id == pitch.ConsultantId);
            if (consultant != null)
            {
                try
                {
                    await _sendbirdService.SendNotificationAsync(consultant.UserId,
                        $"Your pitch on \"{post.Title}\" was declined. {(!string.IsNullOrWhiteSpace(reason) ? $"Reason: {reason}" : "")}",
                        "pitch_rejected");
                }
                catch { }

                try
                {
                    await _emailService.SendGenericNotificationAsync(
                        consultant.Email, $"{consultant.FirstName} {consultant.LastName}",
                        $"Pitch declined — \"{post.Title}\"",
                        "Your pitch was declined",
                        $"<p>Your pitch on \"<strong>{post.Title}</strong>\" was declined.</p>" +
                        (!string.IsNullOrWhiteSpace(reason) ? $"<p><strong>Reason:</strong> {reason}</p>" : ""),
                        "Browse requests", "https://agrichub.io/consultant/requests");
                }
                catch { }
            }

            return await ToPitchResponseAsync(pitch);
        }

        // ── Mapping helpers ─────────────────────────────────────────────────────

        private async Task<OfferPostResponse> ToResponseAsync(OfferPost post, Customer customer)
        {
            var category = post.CategoryId.HasValue
                ? await _categoryRepo.GetSingleByAsync(c => c.Id == post.CategoryId.Value)
                : null;
            var pitchCount = await _pitchRepo.CountAsync(c => c.OfferPostId == post.Id);

            return new OfferPostResponse
            {
                Id = post.Id,
                CustomerId = post.CustomerId,
                CustomerName = $"{customer.FirstName} {customer.LastName}",
                CategoryId = post.CategoryId,
                CategoryName = category?.Name,
                Title = post.Title,
                Description = post.Description,
                Budget = post.Budget,
                PreferredAt = post.PreferredAt,
                Status = post.Status,
                CreatedAt = post.CreatedAt,
                PitchCount = (int)pitchCount,
            };
        }

        private async Task<PitchResponse> ToPitchResponseAsync(CustomOffer pitch)
        {
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.Id == pitch.ConsultantId);
            var reviews = await _reviewRepo.GetAllAsync(r => r.ConsultantId == pitch.ConsultantId);
            var rating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

            return new PitchResponse
            {
                Id = pitch.Id,
                OfferPostId = pitch.OfferPostId ?? Guid.Empty,
                ConsultantId = pitch.ConsultantId,
                ConsultantName = consultant != null ? $"{consultant.FirstName} {consultant.LastName}" : "Unknown",
                ConsultantAvatarUrl = consultant?.AvatarUrl,
                ConsultantRating = Math.Round(rating, 1),
                ServiceId = pitch.ServiceId,
                ServiceName = pitch.Service?.ServiceName,
                Price = pitch.Price,
                Description = pitch.Description,
                PitchMessage = pitch.PitchMessage ?? "",
                PortfolioUrl = pitch.PortfolioUrl,
                PortfolioFileName = pitch.PortfolioFileName,
                IncludesOnsiteVisit = pitch.IncludesOnsiteVisit,
                ScheduledAt = pitch.ScheduledAt ?? DateTime.UtcNow,
                DurationMinutes = pitch.DurationMinutes,
                Status = pitch.Status,
                CreatedAt = pitch.CreatedAt,
                ChatSessionId = pitch.ChatSessionId,
            };
        }
    }
}
