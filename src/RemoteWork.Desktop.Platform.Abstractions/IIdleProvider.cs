namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IIdleProvider
{
    TimeSpan GetIdleTime();
}
