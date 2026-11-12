using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Security;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>A clock the test moves by hand, so idle/absolute expiry and rotation are exact.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
/// In-memory admin sessions that keep the same all-or-nothing behaviour as the SQL statements: a rotation only
/// succeeds for the caller that still holds the current hash, and a revoked row is never changed again.
/// </summary>
internal sealed class InMemoryAdminSessionRepository : IAdminSessionRepository
{
    private readonly object _gate = new();
    private readonly List<AdminSession> _sessions = [];
    private readonly Dictionary<Guid, Admin> _admins = [];

    public IReadOnlyList<AdminSession> All
    {
        get { lock (_gate) { return _sessions.ToList(); } }
    }

    public void AddAdmin(Admin admin) => _admins[admin.Id] = admin;

    public Task AddAsync(AdminSession session, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sessions.Add(session);
            Attach(session);
        }

        return Task.CompletedTask;
    }

    public Task<AdminSession?> FindByHashAsync(string hash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var found = _sessions.FirstOrDefault(s =>
                s.SessionHash == hash || (s.PreviousSessionHash == hash && s.PreviousHashValidUntil > utcNow));
            return Task.FromResult(found is null ? null : Snapshot(found));
        }
    }

    public Task<bool> TryRotateAsync(
        Guid sessionId, string expectedHash, string newHash, DateTime utcNow, DateTime graceUntil, DateTime idleExpiresAt,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId && s.SessionHash == expectedHash && s.RevokedAt is null);
            if (session is null)
            {
                return Task.FromResult(false);
            }

            Set(session, nameof(AdminSession.PreviousSessionHash), session.SessionHash);
            Set(session, nameof(AdminSession.PreviousHashValidUntil), graceUntil);
            Set(session, nameof(AdminSession.SessionHash), newHash);
            Set(session, nameof(AdminSession.RotatedAt), utcNow);
            Set(session, nameof(AdminSession.LastActivityAt), utcNow);
            Set(session, nameof(AdminSession.IdleExpiresAt), idleExpiresAt);
            return Task.FromResult(true);
        }
    }

    public Task TouchAsync(Guid sessionId, DateTime utcNow, DateTime idleExpiresAt, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId && s.RevokedAt is null);
            if (session is not null)
            {
                Set(session, nameof(AdminSession.LastActivityAt), utcNow);
                Set(session, nameof(AdminSession.IdleExpiresAt), idleExpiresAt);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> RevokeAsync(Guid adminId, Guid sessionId, string reason, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId && s.AdminId == adminId && s.RevokedAt is null);
            if (session is null)
            {
                return Task.FromResult(false);
            }

            Revoke(session, reason, utcNow);
            return Task.FromResult(true);
        }
    }

    public Task<int> RevokeAllAsync(Guid adminId, string reason, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var live = _sessions.Where(s => s.AdminId == adminId && s.RevokedAt is null).ToList();
            live.ForEach(s => Revoke(s, reason, utcNow));
            return Task.FromResult(live.Count);
        }
    }

    public Task<int> DeleteEndedBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_sessions.RemoveAll(s => s.AbsoluteExpiresAt < cutoff || s.RevokedAt < cutoff));
        }
    }

    private static void Revoke(AdminSession session, string reason, DateTime utcNow)
    {
        Set(session, nameof(AdminSession.RevokedAt), utcNow);
        Set(session, nameof(AdminSession.RevocationReason), reason);
        Set(session, nameof(AdminSession.PreviousSessionHash), null);
        Set(session, nameof(AdminSession.PreviousHashValidUntil), null);
    }

    private void Attach(AdminSession session)
    {
        if (_admins.TryGetValue(session.AdminId, out var admin))
        {
            Set(session, nameof(AdminSession.Admin), admin);
        }
    }

    // A copy, like a database read: callers never hold the live row the "database" keeps changing.
    private AdminSession Snapshot(AdminSession source)
    {
        var copy = (AdminSession)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AdminSession));
        foreach (var property in typeof(AdminSession).GetProperties().Where(p => p.SetMethod is not null))
        {
            Set(copy, property.Name, property.GetValue(source));
        }

        Attach(copy);
        return copy;
    }

    private static void Set(AdminSession target, string property, object? value) =>
        typeof(AdminSession).GetProperty(property)!.SetValue(target, value);
}

internal static class AdminTestAdmins
{
    public static Admin Active(string email = "boss@example.com")
    {
        var admin = new Admin(email, "hash", AdminRole.SuperAdmin);
        admin.MarkVerified();
        return admin;
    }

    public static void Disable(Admin admin)
    {
        typeof(Admin).GetProperty(nameof(Admin.Status))!.SetValue(admin, AdminStatus.Disabled);
    }

    public static AdminSessionOptions Options() => new()
    {
        CsrfKey = new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()
    };
}
