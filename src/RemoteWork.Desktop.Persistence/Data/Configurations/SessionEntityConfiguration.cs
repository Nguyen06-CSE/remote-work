using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data.Configurations;

public sealed class SessionEntityConfiguration : IEntityTypeConfiguration<SessionEntity>
{
    public void Configure(EntityTypeBuilder<SessionEntity> builder)
    {
        builder.HasKey(e => e.SessionId);

        builder.HasIndex(e => e.DeviceId);
        builder.HasIndex(e => new { e.DeviceId, e.StartedAt });

        builder.Property(e => e.Status).IsRequired();

        // Store timestamps as long (UTC ticks) for SQLite ORDER BY compatibility.
        builder.Property(e => e.StartedAt)
            .HasConversion(DeviceEntityConfiguration.DateTimeOffsetConverter);
        builder.Property(e => e.EndedAt)
            .HasConversion(DeviceEntityConfiguration.NullableDateTimeOffsetConverter);

        builder.HasOne(e => e.Device)
            .WithMany(d => d.Sessions)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
