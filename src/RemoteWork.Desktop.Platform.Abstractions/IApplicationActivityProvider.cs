using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IApplicationActivityProvider
{
    ApplicationActivity? GetActiveApplicationActivity();
}
