using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace CampusLostAndFound.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public string? RegistrationNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Item> ReportedItems { get; set; } = new List<Item>();
        public virtual ICollection<Claim> SubmittedClaims { get; set; } = new List<Claim>();
        public virtual ICollection<CustodyRecord> CustodyRecords { get; set; } = new List<CustodyRecord>();
        public virtual ICollection<Handover> ReceivedHandovers { get; set; } = new List<Handover>();
        public virtual ICollection<Handover> SupervisedHandovers { get; set; } = new List<Handover>();
        public virtual ICollection<DisposalRecord> AuthorizedDisposals { get; set; } = new List<DisposalRecord>();
    }
}
