using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zpi.Database.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_message", table =>
        {
            table.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");
            table.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(300).IsRequired();
        builder.Property(x => x.ContractVersion).HasColumnName("contract_version");
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.Property(x => x.Attempts).HasColumnName("attempts");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredAt }).HasFilter("processed_at IS NULL");
    }
}
