using System;
using System.Collections.Generic;
namespace AgricHub.DAL.Entities.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? UserId { get; set; }
        public string? CountryId { get; set; }
        public string? StateId { get; set; }
        public string? Address { get; set; }
        public bool IsSuspended { get; set; } = false;
        public DateTime? SuspendedAt { get; set; }
        public string? SuspensionReason { get; set; }

        public bool IsVerified { get; set; } = false;
        public int? NoShowCount { get; set; } = 0;
        public string? SendbirdChannelUrl { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Presence ──────────────────────────────────────────────────────────
        public bool IsOnline { get; set; } = false;
        public DateTime? LastSeenAt { get; set; }

        // Navigation properties
        public virtual Wallet? Wallet { get; set; }
        public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
        public virtual ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
    }
}