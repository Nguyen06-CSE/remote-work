using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Repositories;

public sealed class SessionRepository : ISessionRepository
{
    private readonly RemoteWorkDbContext _context;

    public SessionRepository(RemoteWorkDbContext context)
    {
        _context = context;
    }

    public async Task<Session?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        var entity = await _context.Sessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        return entity is null ? null : MapToSession(entity);
    }

    public async Task<IReadOnlyList<Session>> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default)
    {
        var entities = await _context.Sessions.AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(ct);

        return entities.Select(MapToSession).ToList();
    }

    public async Task SaveAsync(Session session, CancellationToken ct = default)
    {
        var entity = new SessionEntity
        {
            SessionId = session.SessionId,
            DeviceId = session.DeviceId,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            Status = session.Status.ToString()
        };
        _context.Sessions.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        var entity = await _context.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, ct);

        if (entity is not null)
        {
            entity.EndedAt = session.EndedAt;
            entity.Status = session.Status.ToString();
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Reconstruct a Session domain object from the persistence entity.
    /// Session.EndedAt and Session.Status have protected setters, so we must
    /// use SessionInfo (the concrete subclass) and call state transition methods
    /// to set the correct status, matching what was persisted.
    /// </summary>
    private static Session MapToSession(SessionEntity entity)
    {
        Enum.TryParse<SessionStatus>(entity.Status, ignoreCase: true, out var status);

        // SessionInfo is the sealed public concrete class; Session base setters are protected.
        var session = new SessionInfo
        {
            SessionId = entity.SessionId,
            DeviceId = entity.DeviceId,
            StartedAt = entity.StartedAt,
        };

        // Drive the session to the persisted state via the domain state machine.
        // Starting is the initial state; we only transition as needed.
        switch (status)
        {
            case SessionStatus.Active:
                session.MarkActive();
                break;
            case SessionStatus.Ending:
                session.MarkActive();
                session.MarkEnding();
                break;
            case SessionStatus.Ended:
                session.MarkActive();
                session.MarkEnded(entity.EndedAt);
                break;
            case SessionStatus.Error:
                session.MarkError();
                break;
            case SessionStatus.Starting:
            default:
                // Already in Starting state — no additional transitions needed.
                break;
        }

        return session;
    }
}
