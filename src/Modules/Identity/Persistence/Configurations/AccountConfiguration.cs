using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Identity.Domain;
using Zpi.Database;

namespace Zpi.Modules.Identity.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("account", table =>
        {
            table.HasCheckConstraint("ck_account_phone", "phone_number ~ '^\\+[1-9][0-9]{1,14}$'");
            table.HasCheckConstraint("ck_account_email_verification", "email_verified_at IS NULL OR email IS NOT NULL");
            table.HasCheckConstraint("ck_account_version", "version >= 1");
            table.HasCheckConstraint("ck_account_status", "status IN ('pendingPhoneVerification','active','blocked')");
            table.HasCheckConstraint("ck_account_first_name_nonempty", "length(first_name) >= 1");
            table.HasCheckConstraint("ck_account_last_name_nonempty", "length(last_name) >= 1");
            table.HasCheckConstraint("ck_account_password_hash_nonempty", "length(password_hash) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email");
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").IsRequired();
        builder.Property(x => x.NormalizedPhoneNumber).HasColumnName("normalized_phone_number").IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(x => x.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<AccountStatus>()).HasMaxLength(40);
        builder.Property(x => x.PhoneVerifiedAt).HasColumnName("phone_verified_at");
        builder.Property(x => x.EmailVerifiedAt).HasColumnName("email_verified_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.NormalizedPhoneNumber).IsUnique();
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(254);
        builder.Property(x => x.PhoneNumber).HasMaxLength(16);
        builder.Property(x => x.NormalizedPhoneNumber).HasMaxLength(16);
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.PasswordHash).HasMaxLength(1024);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
