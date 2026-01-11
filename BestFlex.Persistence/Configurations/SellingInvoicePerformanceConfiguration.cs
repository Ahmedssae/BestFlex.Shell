using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    /// <summary>
    /// Read-heavy indexes to speed up InvoicesPage filters & details loads.
    /// No uniqueness enforced; safe on existing data.
    /// </summary>
    public sealed class SellingInvoicePerformanceConfiguration : IEntityTypeConfiguration<SellingInvoice>
    {
        public void Configure(EntityTypeBuilder<SellingInvoice> b)
        {
            // Common WHERE/ORDER BY fields
            b.HasIndex(i => i.IssuedAt);
            b.HasIndex(i => i.CustomerAccountId);
            b.HasIndex(i => i.InvoiceNo);
        }
    }

    public sealed class SellingInvoiceItemPerformanceConfiguration : IEntityTypeConfiguration<SellingInvoiceItem>
    {
        public void Configure(EntityTypeBuilder<SellingInvoiceItem> b)
        {
            // Speeds up .Include(i => i.Items).ThenInclude(p => p.Product)
            b.HasIndex(x => x.SellingInvoiceId);
            b.HasIndex(x => x.ProductId);
        }
    }
}
