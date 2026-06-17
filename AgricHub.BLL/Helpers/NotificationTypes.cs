using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Helpers
{
    public static class NotificationTypes
    {
        public const string BookingConfirmed = "booking_confirmed";
        public const string BookingRequest = "booking_request";
        public const string BookingCancelled = "booking_cancelled";
        public const string VerificationApproved = "verification_approved";
        public const string VerificationRejected = "verification_rejected";
        public const string CustomOfferReceived = "custom_offer_received";
        public const string CustomOfferAccepted = "custom_offer_accepted";
        public const string CustomOfferRejected = "custom_offer_rejected";
        public const string WalletTopUp = "wallet_topup";
        public const string PayoutInitiated = "payout_initiated";
        public const string ReviewReceived = "review_received";
    }
}


