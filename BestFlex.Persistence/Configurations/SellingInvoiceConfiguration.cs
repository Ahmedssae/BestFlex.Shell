using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public class SellingInvoiceConfiguration : IEntityTypeConfiguration<SellingInvoice>
    {
        public void Configure(EntityTypeBuilder<SellingInvoice> b)
        {
            b.ToTable("SellingInvoices");
            b.HasKey(x => x.Id);

            b.Property(x => x.InvoiceNo)
                .IsRequired()
                .HasMaxLength(20);

            b.HasIndex(x => x.InvoiceNo)
                .IsUnique(); // each invoice number unique

            b.Property(x => x.IssuedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP"); // for SQLite

            b.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(10);

            b.Property(x => x.Issuer)
                .IsRequired()
                .HasMaxLength(60);

            b.Property(x => x.Description)
                .HasMaxLength(400);

            // Relationship to Customer
            b.HasOne(x => x.CustomerAccount)
             .WithMany(c => c.Invoices)
             .HasForeignKey(x => x.CustomerAccountId)
             .OnDelete(DeleteBehavior.Restrict); // prevent deleting customer if they have invoices
        }
    }
}
