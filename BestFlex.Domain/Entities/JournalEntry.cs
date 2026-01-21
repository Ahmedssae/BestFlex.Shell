using System;
using System.Collections.Generic;

namespace BestFlex.Domain.Entities
{
    public class JournalEntry
    {
        public int Id { get; set; }
        public DateTime EntryDate { get; set; } = DateTime.UtcNow;
        public ReferenceType ReferenceType { get; set; }
        public int ReferenceId { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
    }

    public enum ReferenceType
    {
        Invoice = 1,
        Stock = 2,
        Adjustment = 3
    }
}
