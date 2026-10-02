using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests that the database schema is created correctly and the schema matches expectations.
/// </summary>
public sealed class DatabaseCreationTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();

    [Fact]
    public void Database_Should_Create_Successfully()
    {
        // EnsureCreated is called in DbContextFixture constructor.
        // We verify the DB file exists by confirming we can open a connection.
        Assert.True(_fixture.Context.Database.CanConnect());
    }

    [Fact]
    public async Task All_Tables_Should_Exist_After_Creation()
    {
        // Verify each entity set can be queried without error (table exists).
        var deviceCount = await _fixture.Context.Devices.CountAsync();
        var sessionCount = await _fixture.Context.Sessions.CountAsync();
        var batchCount = await _fixture.Context.ActivityBatches.CountAsync();
        var activityCount = await _fixture.Context.ApplicationActivities.CountAsync();
        var syncCount = await _fixture.Context.SyncQueue.CountAsync();

        Assert.Equal(0, deviceCount);
        Assert.Equal(0, sessionCount);
        Assert.Equal(0, batchCount);
        Assert.Equal(0, activityCount);
        Assert.Equal(0, syncCount);
    }

    [Fact]
    public async Task EnsureCreated_Is_Idempotent_On_Existing_Database()
    {
        // First creation happens in fixture constructor.
        // Second call should succeed without errors.
        var result = await _fixture.Context.Database.EnsureCreatedAsync();

        // Returns false when DB already exists (not created again) — no exception.
        Assert.False(result);
        Assert.True(_fixture.Context.Database.CanConnect());
    }

    [Fact]
    public async Task MigrateAsync_Should_Apply_Migrations_To_Empty_Database()
    {
        // Create a fresh DB and apply migrations (as production code does via DatabaseInitializer).
        var tempPath = Path.Combine(Path.GetTempPath(), $"rw_migration_test_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<RemoteWorkDbContext>()
                .UseSqlite($"Data Source={tempPath}")
                .Options;

            await using var ctx = new RemoteWorkDbContext(options);
            await ctx.Database.MigrateAsync();

            Assert.True(ctx.Database.CanConnect());
            Assert.Equal(0, await ctx.Devices.CountAsync());
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public void Dispose() => _fixture.Dispose();
}
