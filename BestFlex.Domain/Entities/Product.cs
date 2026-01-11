using System;

namespace BestFlex.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }

        // Keep as decimal if you plan fractional quantities; otherwise int is fine.
        public decimal StockQty { get; set; }

        // ✅ SQLite-friendly optimistic concurrency token.
        // We manually increment to ensure EF’s WHERE clause detects conflicts.
        public int Version { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
