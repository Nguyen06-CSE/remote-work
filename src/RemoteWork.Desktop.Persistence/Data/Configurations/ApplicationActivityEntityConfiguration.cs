using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data.Configurations;

public sealed class ApplicationActivityEntityConfiguration : IEntityTypeConfiguration<ApplicationActivityEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationActivityEntity> builder)
    {
        builder.HasKey(e => e.ActivityId);

        builder.HasIndex(e => new { e.DeviceId, e.Timestamp }).IsDescending(false, true);
        builder.HasIndex(e => e.SessionId);

        // Store Timestamp as long (UTC ticks) for SQLite ORDER BY compatibility.
        builder.Property(e => e.Timestamp)
            .HasConversion(DeviceEntityConfiguration.DateTimeOffsetConverter);

        builder.HasOne(e => e.Device)
            .WithMany(d => d.ApplicationActivities)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Session)
            .WithMany(s => s.ApplicationActivities)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
