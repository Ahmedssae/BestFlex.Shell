using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BestFlex.Domain.Entities;

namespace BestFlex.Persistence.Configurations
{
    public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
    {
        public void Configure(EntityTypeBuilder<SalesOrder> builder)
        {
            builder.ToTable("SalesOrders");
            
            builder.HasKey(so => so.Id);
            
            builder.Property(so => so.OrderNumber)
                .IsRequired()
                .HasMaxLength(50);
            
            builder.Property(so => so.Notes)
                .HasMaxLength(1000);
            
            builder.Property(so => so.TotalAmount)
                .HasPrecision(18, 2);
            
            builder.Property(so => so.TaxAmount)
                .HasPrecision(18, 2);
            
            builder.Property(so => so.CreatedAt)
                .IsRequired();
            
            builder.Property(so => so.UpdatedAt);
            
            builder.Property(so => so.InvoiceId);
            
            // Relationships
            builder.HasOne<CustomerAccount>()
                .WithMany()
                .HasForeignKey(so => so.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes
            builder.HasIndex(so => so.OrderNumber)
                .IsUnique();
            
            builder.HasIndex(so => so.CustomerId);
            
            builder.HasIndex(so => so.Status);
            
            builder.HasIndex(so => so.OrderDate);
            
            builder.HasIndex(so => so.InvoiceId)
                .IsUnique()
                .HasFilter("InvoiceId IS NOT NULL");
        }
    }

    public class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
    {
        public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
        {
            builder.ToTable("SalesOrderLines");
            
            builder.HasKey(sol => sol.Id);
            
            builder.Property(sol => sol.Quantity)
                .HasPrecision(18, 4);
            
            builder.Property(sol => sol.UnitPrice)
                .HasPrecision(18, 2);
            
            builder.Property(sol => sol.Discount)
                .HasPrecision(5, 2);
            
            // Relationships
            builder.HasOne<SalesOrder>()
                .WithMany(so => so.Lines)
                .HasForeignKey(sol => sol.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(sol => sol.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes
            builder.HasIndex(sol => sol.SalesOrderId);
            
            builder.HasIndex(sol => sol.ProductId);
        }
    }

    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("Invoices");
            
            builder.HasKey(i => i.Id);
            
            builder.Property(i => i.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);
            
            builder.Property(i => i.Notes)
                .HasMaxLength(1000);
            
            builder.Property(i => i.Subtotal)
                .HasPrecision(18, 2);
            
            builder.Property(i => i.TaxAmount)
                .HasPrecision(18, 2);
            
            builder.Property(i => i.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("USD");
            
            builder.Property(i => i.CreatedAt)
                .IsRequired();
            
            builder.Property(i => i.PostedAt);
            
            builder.Property(i => i.UpdatedAt);
            
            // Relationships
            builder.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(i => i.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes
            builder.HasIndex(i => i.InvoiceNumber)
                .IsUnique();
            
            builder.HasIndex(i => i.SalesOrderId)
                .IsUnique();
            
            builder.HasIndex(i => i.Status);
            
            builder.HasIndex(i => i.InvoiceDate);
            
            builder.HasIndex(i => i.DueDate);
        }
    }

    public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
    {
        public void Configure(EntityTypeBuilder<InvoiceLine> builder)
        {
            builder.ToTable("InvoiceLines");
            
            builder.HasKey(il => il.Id);
            
            builder.Property(il => il.ProductDescription)
                .IsRequired()
                .HasMaxLength(500);
            
            builder.Property(il => il.Quantity)
                .HasPrecision(18, 4);
            
            builder.Property(il => il.UnitPrice)
                .HasPrecision(18, 2);
            
            builder.Property(il => il.TaxRate)
                .HasPrecision(5, 4);
            
            // Relationships
            builder.HasOne<Invoice>()
                .WithMany(i => i.Lines)
                .HasForeignKey(il => il.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(il => il.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes
            builder.HasIndex(il => il.InvoiceId);
            
            builder.HasIndex(il => il.ProductId);
        }
    }
}
