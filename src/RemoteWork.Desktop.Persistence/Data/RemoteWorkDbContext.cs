using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Data;

public sealed class RemoteWorkDbContext : DbContext
{
    public RemoteWorkDbContext(DbContextOptions<RemoteWorkDbContext> options) : base(options) { }

    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<ActivityBatchEntity> ActivityBatches => Set<ActivityBatchEntity>();
    public DbSet<ApplicationActivityEntity> ApplicationActivities => Set<ApplicationActivityEntity>();
    public DbSet<SyncQueueItemEntity> SyncQueue => Set<SyncQueueItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RemoteWorkDbContext).Assembly);
    }
}
