using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Database;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("role", table =>
        {
            table.HasCheckConstraint("ck_role_code", "code IN ('senior','caretaker')");
        });
        builder.HasData(
            new Role { Id = Guid.Parse("019a0000-0000-7000-8000-000000000001"), Code = AccountRole.Senior, Name = "Senior" },
            new Role { Id = Guid.Parse("019a0000-0000-7000-8000-000000000002"), Code = AccountRole.Caretaker, Name = "Caretaker" });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Code).HasColumnName("code").HasConversion(new CamelCaseEnumConverter<AccountRole>()).HasMaxLength(40);
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}
