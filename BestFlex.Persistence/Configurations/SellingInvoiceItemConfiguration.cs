using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public class SellingInvoiceItemConfiguration : IEntityTypeConfiguration<SellingInvoiceItem>
    {
        public void Configure(EntityTypeBuilder<SellingInvoiceItem> b)
        {
            b.ToTable("SellingInvoiceItems");
            b.HasKey(x => x.Id);

            b.Property(x => x.Quantity)
                .HasPrecision(18, 3);

            b.Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            // Relationship to SellingInvoice
            b.HasOne(x => x.SellingInvoice)
             .WithMany(i => i.Items)
             .HasForeignKey(x => x.SellingInvoiceId)
             .OnDelete(DeleteBehavior.Cascade); // delete items when invoice is deleted

            // Relationship to Product
            b.HasOne(x => x.Product)
             .WithMany() // Product doesn’t need to list all invoice items
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Restrict); // can’t delete product if used in invoice
        }
    }
}
