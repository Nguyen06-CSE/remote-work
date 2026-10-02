using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data.Configurations;

public sealed class ActivityBatchEntityConfiguration : IEntityTypeConfiguration<ActivityBatchEntity>
{
    public void Configure(EntityTypeBuilder<ActivityBatchEntity> builder)
    {
        builder.HasKey(e => e.BatchId);

        builder.HasIndex(e => new { e.SessionId, e.StartedAt });
        builder.HasIndex(e => new { e.DeviceId, e.StartedAt });

        // Store timestamps as long (UTC ticks) for SQLite ORDER BY compatibility.
        builder.Property(e => e.StartedAt)
            .HasConversion(DeviceEntityConfiguration.DateTimeOffsetConverter);
        builder.Property(e => e.EndedAt)
            .HasConversion(DeviceEntityConfiguration.DateTimeOffsetConverter);

        builder.HasOne(e => e.Device)
            .WithMany(d => d.ActivityBatches)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Session)
            .WithMany(s => s.ActivityBatches)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
