using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BestFlex.Persistence.Configurations
{
    public class UsersConfiguration : IEntityTypeConfiguration<Users>
    {
        public void Configure(EntityTypeBuilder<Users> b)
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);

            b.Property(x => x.Username)
                .IsRequired()
                .HasMaxLength(64);

            b.Property(x => x.DisplayName)
                .HasMaxLength(128);

            b.Property(x => x.PasswordHash)
                .IsRequired();

            b.Property(x => x.RolesCsv)
                .HasMaxLength(256);

            // Default timestamps (Sqlite-safe)
            // If you’re on SQL Server Online mode, this will be ignored at runtime
            b.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
