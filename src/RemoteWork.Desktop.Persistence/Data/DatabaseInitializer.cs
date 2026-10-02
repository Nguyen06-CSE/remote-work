using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RemoteWork.Desktop.Persistence.Data;

public sealed class DatabaseInitializer
{
    private readonly RemoteWorkDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(RemoteWorkDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Applying database migrations...");
        await _context.Database.MigrateAsync(ct);
        _logger.LogInformation("Database ready.");
    }
}
