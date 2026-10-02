using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data.Configurations;

public sealed class SyncQueueItemEntityConfiguration : IEntityTypeConfiguration<SyncQueueItemEntity>
{
    public void Configure(EntityTypeBuilder<SyncQueueItemEntity> builder)
    {
        builder.HasKey(e => e.ItemId);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.Status, e.CreatedAt });

        // Store timestamps as long (UTC ticks) for SQLite ORDER BY compatibility.
        builder.Property(e => e.CreatedAt)
            .HasConversion(DeviceEntityConfiguration.DateTimeOffsetConverter);
        builder.Property(e => e.SentAt)
            .HasConversion(DeviceEntityConfiguration.NullableDateTimeOffsetConverter);
    }
}
