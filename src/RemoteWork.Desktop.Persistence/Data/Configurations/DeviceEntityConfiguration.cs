using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data.Configurations;

public sealed class DeviceEntityConfiguration : IEntityTypeConfiguration<DeviceEntity>
{
    // Reusable converter: DateTimeOffset <-> long (UTC ticks). Enables SQLite ORDER BY on timestamps.
    internal static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter =
        new(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));

    internal static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter =
        new(v => v == null ? null : (long?)v.Value.UtcTicks,
            v => v == null ? null : (DateTimeOffset?)new DateTimeOffset(v.Value, TimeSpan.Zero));

    public void Configure(EntityTypeBuilder<DeviceEntity> builder)
    {
        builder.HasKey(e => e.DeviceId);

        builder.Property(e => e.Hostname).IsRequired();
        builder.Property(e => e.OperatingSystem).IsRequired();
        builder.Property(e => e.OsVersion).IsRequired();
        builder.Property(e => e.AgentVersion).IsRequired();

        // Store timestamps as long (UTC ticks) so SQLite can sort and index them.
        builder.Property(e => e.RegisteredAt).HasConversion(DateTimeOffsetConverter);
        builder.Property(e => e.LastSeenAt).HasConversion(DateTimeOffsetConverter);

        builder.HasMany(e => e.Sessions)
            .WithOne(e => e.Device)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ActivityBatches)
            .WithOne(e => e.Device)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ApplicationActivities)
            .WithOne(e => e.Device)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
