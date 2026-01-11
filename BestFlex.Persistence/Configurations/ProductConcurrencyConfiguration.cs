using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    /// <summary>
    /// Adds a shadow concurrency token to Product without touching the entity class.
    /// </summary>
    public sealed class ProductConcurrencyConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> b)
        {
            b.Property<byte[]>("RowVersion").IsRowVersion();
        }
    }
}
