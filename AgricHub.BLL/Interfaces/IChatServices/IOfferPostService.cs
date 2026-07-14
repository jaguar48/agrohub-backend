using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IChatServices
{
    /// <summary>
    /// Fiverr-style flow: customer posts a request (OfferPost), any consultant
    /// browsing open requests can submit a pitch (CustomOffer w/ pitch fields).
    /// Every pitch also drops into the customer↔consultant chat as a card,
    /// same as the existing single-consultant custom offer flow.
    /// </summary>
    public interface IOfferPostService
    {
        Task<OfferPostResponse> CreatePostAsync(CreateOfferPostRequest request);
        Task<IEnumerable<OfferPostResponse>> GetOpenPostsAsync(int? categoryId = null);
        Task<IEnumerable<OfferPostResponse>> GetMyPostsAsync();
        Task<OfferPostDetailResponse> GetPostDetailAsync(Guid postId);
        Task ClosePostAsync(Guid postId);

        Task<PitchResponse> SubmitPitchAsync(SubmitPitchRequest request, IFormFile? portfolioFile);
        Task<IEnumerable<PitchResponse>> GetMyPitchesAsync();

        /// <summary>Customer accepts one pitch — creates the Consultation/escrow
        /// (reuses the existing accept logic) and closes the post + rejects the rest.</summary>
        Task<PitchResponse> AcceptPitchAsync(Guid pitchId);
        Task<PitchResponse> RejectPitchAsync(Guid pitchId, string reason);
    }
}
