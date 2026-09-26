namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IIdleTimeProvider
{
    TimeSpan GetIdleTime();
}
