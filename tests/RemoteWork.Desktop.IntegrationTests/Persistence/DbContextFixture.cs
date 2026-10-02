using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Persistence.Data;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Creates an isolated file-based SQLite database per test instance.
/// Using a real file (not :memory:) provides a more realistic test environment
/// and allows testing migrations and connection lifecycle (restart behavior).
/// </summary>
public sealed class DbContextFixture : IDisposable
{
    private readonly string _dbPath;

    public RemoteWorkDbContext Context { get; }

    public DbContextFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"rw_test_{Guid.NewGuid():N}.db");
        Context = CreateContext(_dbPath);
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a fresh context pointing at the same database file.
    /// Use this to simulate application restart (dispose original, reopen same file).
    /// </summary>
    public RemoteWorkDbContext ReopenContext() => CreateContext(_dbPath);

    private static RemoteWorkDbContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<RemoteWorkDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new RemoteWorkDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var walPath = _dbPath + "-wal";
            if (File.Exists(walPath)) File.Delete(walPath);
            var shmPath = _dbPath + "-shm";
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
        catch
        {
            // Best-effort cleanup — test isolation is not broken by leftover temp files.
        }
    }
}
