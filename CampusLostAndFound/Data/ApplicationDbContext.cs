using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CampusLostAndFound.Models;

namespace CampusLostAndFound.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
        public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
        public DbSet<CampusLocation> CampusLocations => Set<CampusLocation>();
        public DbSet<PrivateItemDetail> PrivateItemDetails => Set<PrivateItemDetail>();
        public DbSet<MatchSuggestion> MatchSuggestions => Set<MatchSuggestion>();
        public DbSet<Claim> Claims => Set<Claim>();
        public DbSet<VerificationChallenge> VerificationChallenges => Set<VerificationChallenge>();
        public DbSet<CustodyRecord> CustodyRecords => Set<CustodyRecord>();
        public DbSet<Handover> Handovers => Set<Handover>();
        public DbSet<DisposalRecord> DisposalRecords => Set<DisposalRecord>();
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Item relationship configurations
            builder.Entity<Item>(entity =>
            {
                entity.HasOne(i => i.Reporter)
                    .WithMany(u => u.ReportedItems)
                    .HasForeignKey(i => i.ReporterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Category)
                    .WithMany(c => c.Items)
                    .HasForeignKey(i => i.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Location)
                    .WithMany(l => l.Items)
                    .HasForeignKey(i => i.LocationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.PrivateDetail)
                    .WithOne(p => p.Item)
                    .HasForeignKey<PrivateItemDetail>(p => p.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => i.ReportType);
                entity.HasIndex(i => i.Status);
                entity.HasIndex(i => i.CategoryId);
                entity.HasIndex(i => i.LocationId);
                entity.HasIndex(i => i.ItemDateTime);
                entity.HasIndex(i => i.CreatedAt);
            });

            // MatchSuggestion configurations
            builder.Entity<MatchSuggestion>(entity =>
            {
                entity.HasOne(m => m.LostItem)
                    .WithMany(i => i.MatchSuggestionsAsLost)
                    .HasForeignKey(m => m.LostItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.FoundItem)
                    .WithMany(i => i.MatchSuggestionsAsFound)
                    .HasForeignKey(m => m.FoundItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => new { m.LostItemId, m.FoundItemId }).IsUnique();
                entity.HasIndex(m => m.MatchScore);
            });

            // Claim configurations
            builder.Entity<Claim>(entity =>
            {
                entity.HasOne(c => c.Item)
                    .WithMany(i => i.Claims)
                    .HasForeignKey(c => c.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Claimant)
                    .WithMany(u => u.SubmittedClaims)
                    .HasForeignKey(c => c.ClaimantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Reviewer)
                    .WithMany()
                    .HasForeignKey(c => c.ReviewedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.Status);
                entity.HasIndex(c => c.SubmittedAt);
            });

            // VerificationChallenge configurations
            builder.Entity<VerificationChallenge>(entity =>
            {
                entity.HasOne(v => v.Claim)
                    .WithMany(c => c.Challenges)
                    .HasForeignKey(v => v.ClaimId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CustodyRecord configurations
            builder.Entity<CustodyRecord>(entity =>
            {
                entity.HasOne(c => c.Item)
                    .WithMany(i => i.CustodyRecords)
                    .HasForeignKey(c => c.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ResponsibleSecurityUser)
                    .WithMany(u => u.CustodyRecords)
                    .HasForeignKey(c => c.ResponsibleSecurityUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.ReceivedAt);
            });

            // Handover configurations
            builder.Entity<Handover>(entity =>
            {
                entity.HasOne(h => h.Item)
                    .WithOne(i => i.Handover)
                    .HasForeignKey<Handover>(h => h.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(h => h.Claim)
                    .WithOne(c => c.Handover)
                    .HasForeignKey<Handover>(h => h.ClaimId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(h => h.ReceivingUser)
                    .WithMany(u => u.ReceivedHandovers)
                    .HasForeignKey(h => h.ReceivingUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(h => h.SecurityUser)
                    .WithMany(u => u.SupervisedHandovers)
                    .HasForeignKey(h => h.SecurityUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DisposalRecord configurations
            builder.Entity<DisposalRecord>(entity =>
            {
                entity.HasOne(d => d.Item)
                    .WithOne(i => i.DisposalRecord)
                    .HasForeignKey<DisposalRecord>(d => d.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.AuthorizedByUser)
                    .WithMany(u => u.AuthorizedDisposals)
                    .HasForeignKey(d => d.AuthorizedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // AuditEntry index
            builder.Entity<AuditEntry>(entity =>
            {
                entity.HasIndex(a => a.Timestamp);
                entity.HasIndex(a => a.TargetRecord);
            });
        }
    }
}
