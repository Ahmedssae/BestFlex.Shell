using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public sealed class InvoiceNoSequenceConfiguration : IEntityTypeConfiguration<InvoiceNoSequence>
    {
        public void Configure(EntityTypeBuilder<InvoiceNoSequence> b)
        {
            b.ToTable("InvoiceNoSequences");
            b.HasKey(x => x.Id);
            b.Property(x => x.YearMonth).IsRequired().HasMaxLength(6);
            b.Property(x => x.Next).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.CompanyId, x.YearMonth }).IsUnique();
        }
    }
}
