using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgricHub.DAL.Context
{
    public class AgricHubDbContext : IdentityDbContext<ApplicationUser>
    {
        public AgricHubDbContext(DbContextOptions<AgricHubDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============= CONSULTATION RELATIONSHIPS =============

            modelBuilder.Entity<Consultation>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Consultation>()
                .HasOne(c => c.Consultant)
                .WithMany()
                .HasForeignKey(c => c.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Consultation>()
                .HasOne(c => c.Service)
                .WithMany()
                .HasForeignKey(c => c.ServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Consultation>()
                .HasOne(c => c.ServicePackage)
                .WithMany()
                .HasForeignKey(c => c.ServicePackageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // ============= CHAT SESSION RELATIONSHIPS =============

            modelBuilder.Entity<ChatSession>()
                .HasOne(cs => cs.Customer)
                .WithMany()
                .HasForeignKey(cs => cs.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatSession>()
                .HasOne(cs => cs.Consultant)
                .WithMany()
                .HasForeignKey(cs => cs.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatSession>()
                .HasOne(cs => cs.Service)
                .WithMany()
                .HasForeignKey(cs => cs.ServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // ============= REVIEW RELATIONSHIPS =============

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Consultation)
                .WithOne()
                .HasForeignKey<Review>(r => r.ConsultationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Consultant)
                .WithMany()
                .HasForeignKey(r => r.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Service)
                .WithMany()
                .HasForeignKey(r => r.ServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // ============= WALLET RELATIONSHIPS =============

            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.Customer)
                .WithMany()
                .HasForeignKey(w => w.CustomerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.Consultant)
                .WithMany()
                .HasForeignKey(w => w.ConsultantId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Wallet>()
                .HasCheckConstraint("CK_Wallet_CustomerOrConsultant",
                    "(CustomerId IS NOT NULL AND ConsultantId IS NULL) OR (CustomerId IS NULL AND ConsultantId IS NOT NULL)");

            // ============= WALLET TRANSACTION RELATIONSHIPS =============

            modelBuilder.Entity<WalletTransaction>()
                .HasOne(wt => wt.Customer)
                .WithMany(c => c.WalletTransactions)
                .HasForeignKey(wt => wt.CustomerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WalletTransaction>()
                .HasOne(wt => wt.Consultant)
                .WithMany(c => c.WalletTransactions)
                .HasForeignKey(wt => wt.ConsultantId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // ============= PENDING TRANSACTION RELATIONSHIPS =============

            modelBuilder.Entity<PendingTransaction>()
                .HasOne(pt => pt.Customer)
                .WithMany()
                .HasForeignKey(pt => pt.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PendingTransaction>()
                .HasOne(pt => pt.Consultation)
                .WithMany()
                .HasForeignKey(pt => pt.ConsultationId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============= SERVICE PACKAGE RELATIONSHIPS =============

            modelBuilder.Entity<ServicePackage>()
                .HasOne(sp => sp.Service)
                .WithMany(s => s.Packages)
                .HasForeignKey(sp => sp.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============= CUSTOM OFFER RELATIONSHIPS =============

            modelBuilder.Entity<CustomOffer>()
                .HasOne(co => co.ChatSession)
                .WithMany()
                .HasForeignKey(co => co.ChatSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomOffer>()
                .HasOne(co => co.Service)
                .WithMany()
                .HasForeignKey(co => co.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============= SERVICE RELATIONSHIPS =============

            // ← CHANGED: WithMany(b => b.Services) so Business.Services navigation works
            modelBuilder.Entity<Service>()
                .HasOne(s => s.Business)
                .WithMany(b => b.Services)
                .HasForeignKey(s => s.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Category)
                .WithMany()
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============= BUSINESS RELATIONSHIPS =============

            modelBuilder.Entity<Business>()
                .HasOne(b => b.Consultant)
                .WithMany()
                .HasForeignKey(b => b.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============= DECIMAL PRECISION =============

            modelBuilder.Entity<Wallet>()
                .Property(w => w.Balance)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<WalletTransaction>()
                .Property(wt => wt.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PendingTransaction>()
                .Property(pt => pt.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ServicePackage>()
                .Property(sp => sp.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CustomOffer>()
                .Property(co => co.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Consultation>()
                .Property(c => c.CustomPrice)
                .HasColumnType("decimal(18,2)");
        }

        // ============= DBSETS =============

        public DbSet<Consultant> Consultants { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServicePackage> ServicePackages { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<PendingTransaction> PendingTransactions { get; set; }
        public DbSet<Business> Businesses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Consultation> Consultations { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<CustomOffer> CustomOffers { get; set; }
        public DbSet<BusinessVerification> BusinessVerifications { get; set; }  // ← ADDED
                                                                                // In AgricHubDbContext.cs — add this DbSet:
        public DbSet<PlatformSetting> PlatformSettings { get; set; }
    }
}