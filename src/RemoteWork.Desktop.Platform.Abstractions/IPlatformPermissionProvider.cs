namespace RemoteWork.Desktop.Platform.Abstractions;

public enum PermissionStatus { Unknown, Granted, Denied, NotRequired }

public sealed class PermissionInfo
{
    public string CapabilityName { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public PermissionStatus Status { get; init; }
    public string HowToGrant { get; init; } = string.Empty;
}

public interface IPlatformPermissionProvider
{
    IReadOnlyList<PermissionInfo> GetPermissions();
}
