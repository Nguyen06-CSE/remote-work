namespace RemoteWork.Agent.Core.Interfaces;

public interface IDeviceIdentityStore
{
    string GetOrCreateDeviceId();
}