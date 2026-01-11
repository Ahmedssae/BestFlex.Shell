using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public sealed class PrintTemplateVersionConfiguration : IEntityTypeConfiguration<PrintTemplateVersion>
    {
        public void Configure(EntityTypeBuilder<PrintTemplateVersion> b)
        {
            b.ToTable("PrintTemplateVersions");
            b.HasKey(x => x.Id);

            b.Property(x => x.DocType).IsRequired().HasMaxLength(32);
            b.Property(x => x.Engine).IsRequired().HasMaxLength(32);
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();

            b.HasOne(x => x.Company)
             .WithMany()
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.CompanyId, x.DocType, x.CreatedAtUtc });
        }
    }
}
