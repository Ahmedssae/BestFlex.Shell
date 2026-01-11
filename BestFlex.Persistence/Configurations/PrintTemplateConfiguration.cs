using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public sealed class PrintTemplateConfiguration : IEntityTypeConfiguration<PrintTemplate>
    {
        public void Configure(EntityTypeBuilder<PrintTemplate> b)
        {
            b.ToTable("PrintTemplates");
            b.HasKey(x => x.Id);

            b.Property(x => x.DocType).IsRequired().HasMaxLength(32);
            b.Property(x => x.Engine).IsRequired().HasMaxLength(32);
            b.Property(x => x.Payload).IsRequired();

            b.HasOne(x => x.Company)
             .WithMany()
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);

            // Useful filtered lookups
            b.HasIndex(x => new { x.CompanyId, x.DocType, x.IsDefault });
        }
    }
}
