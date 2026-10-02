namespace RemoteWork.Desktop.Persistence.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string? DatabaseDirectory { get; set; } // null = use platform default
    public string DatabaseFileName { get; set; } = "remotework.db";
}
