using DigitalTwin.Domain.Interfaces;

namespace DigitalTwin.Infrastructure.Policies;

/// <summary>
/// Identifies transient infrastructure failures caused by cloud DB unavailability
/// or network loss, signalling that a local-fallback should be used instead of
/// surfacing the error to callers.
/// </summary>
public sealed class NpgsqlTransientFailurePolicy : ITransientFailurePolicy
{
    public bool IsTransient(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var name = e.GetType().FullName ?? string.Empty;
            if (name.Contains("NpgsqlException") ||
                e is System.Net.Sockets.SocketException)
                return true;
        }
        return false;
    }
}
