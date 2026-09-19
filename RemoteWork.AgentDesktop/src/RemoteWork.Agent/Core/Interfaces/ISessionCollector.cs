// src/RemoteWork.Agent/Collectors/Session/ISessionCollector.cs

using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Core.Interfaces;

public interface ISessionCollector
{
    SessionInfo StartSession(string deviceId);

    void EndSession();

    SessionInfo? GetCurrentSession();
}