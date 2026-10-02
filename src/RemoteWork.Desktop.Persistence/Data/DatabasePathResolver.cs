using Microsoft.Extensions.Options;

namespace RemoteWork.Desktop.Persistence.Data;

public sealed class DatabasePathResolver
{
    private readonly DatabaseOptions _options;

    public DatabasePathResolver(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public string ResolveDatabasePath()
    {
        var directory = _options.DatabaseDirectory ?? GetPlatformDefaultDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, _options.DatabaseFileName);
    }

    private static string GetPlatformDefaultDirectory()
    {
        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        if (string.IsNullOrEmpty(appData))
        {
            appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create);
        }

        return Path.Combine(appData, "RemoteWork", "Agent");
    }
}
