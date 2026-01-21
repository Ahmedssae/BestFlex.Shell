using System;

namespace BestFlex.Domain.Entities
{
    public class JournalLine
    {
        public int Id { get; set; }
        public int JournalEntryId { get; set; }
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public Account Account { get; set; } = null!;
        public JournalEntry JournalEntry { get; set; } = null!;
    }
}
