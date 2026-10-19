using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Infrastructure.Persistence.Configurations;

internal sealed class SosTriggerReceiptConfiguration : IEntityTypeConfiguration<SosTriggerReceipt>
{
    public void Configure(EntityTypeBuilder<SosTriggerReceipt> builder)
    {
        builder.ToTable("sos_trigger_receipt");
        builder.HasKey(row => new { row.DeviceId, row.TriggeredAt });
        builder.Property(row => row.DeviceId).HasColumnName("device_id").ValueGeneratedNever();
        builder.Property(row => row.TriggeredAt).HasColumnName("triggered_at");
        builder.Property(row => row.IncidentId).HasColumnName("incident_id");
        builder.HasOne<Device>().WithMany().HasForeignKey(row => row.DeviceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SosIncident>().WithMany().HasForeignKey(row => row.IncidentId).OnDelete(DeleteBehavior.Cascade);
    }
}
