using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
    {
        public void Configure(EntityTypeBuilder<CustomerAccount> b)
        {
            b.ToTable("CustomerAccounts");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(120);

            b.Property(x => x.Phone)
                .HasMaxLength(30);

            b.Property(x => x.Balance)
                .HasPrecision(18, 2)
                .HasDefaultValue(0);
        }
    }
}
