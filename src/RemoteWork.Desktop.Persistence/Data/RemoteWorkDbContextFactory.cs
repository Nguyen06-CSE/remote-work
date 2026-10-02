using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RemoteWork.Desktop.Persistence.Data;

/// <summary>
/// Design-time factory for EF Core tooling (migrations). Not used at runtime.
/// </summary>
public sealed class RemoteWorkDbContextFactory : IDesignTimeDbContextFactory<RemoteWorkDbContext>
{
    public RemoteWorkDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RemoteWorkDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new RemoteWorkDbContext(options);
    }
}
