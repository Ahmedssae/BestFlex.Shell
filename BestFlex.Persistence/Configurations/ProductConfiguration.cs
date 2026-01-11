using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> b)
        {
            b.ToTable("Products");
            b.HasKey(x => x.Id);

            b.Property(x => x.Code).HasMaxLength(64).IsRequired();
            b.Property(x => x.Name).HasMaxLength(256).IsRequired();
            b.Property(x => x.Price).HasColumnType("decimal(18,2)");
            b.Property(x => x.StockQty).HasColumnType("decimal(18,3)");

            // ✅ Concurrency
            b.Property(x => x.Version)
             .IsConcurrencyToken();

            b.HasIndex(x => x.Code).IsUnique();
        }
    }
}
