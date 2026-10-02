using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data.Configurations;

public sealed class SyncQueueItemEntityConfiguration : IEntityTypeConfiguration<SyncQueueItemEntity>
{
    public void Configure(EntityTypeBuilder<SyncQueueItemEntity> builder)
    {
        builder.HasKey(e => e.QueueItemId);

        // Ignore computed backward-compatibility aliases
        builder.Ignore(e => e.ItemId);
        builder.Ignore(e => e.SentAt);
        builder.Ignore(e => e.FailureReason);
        builder.Ignore(e => e.RetryCount);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
        builder.HasIndex(e => new { e.EntityType, e.EntityId });

        // Store timestamps as long (UTC ticks) for SQLite ORDER BY compatibility.
        builder.Property(e => e.CreatedAt)
            .HasConversion(DeviceEntityConfiguration.DateTimeOffsetConverter);
        builder.Property(e => e.LastAttemptAt)
            .HasConversion(DeviceEntityConfiguration.NullableDateTimeOffsetConverter);
        builder.Property(e => e.NextAttemptAt)
            .HasConversion(DeviceEntityConfiguration.NullableDateTimeOffsetConverter);
    }
}
