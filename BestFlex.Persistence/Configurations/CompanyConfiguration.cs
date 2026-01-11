using BestFlex.Domain;
using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> b)
        {
            b.ToTable("Companies");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(120);

            b.Property(x => x.IsOnlineDbEnabled)
                .HasDefaultValue(false);
        }
    }
}
