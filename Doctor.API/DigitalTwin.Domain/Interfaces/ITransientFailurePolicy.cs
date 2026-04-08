namespace DigitalTwin.Domain.Interfaces;

/// <summary>
/// Determines whether an exception represents a transient infrastructure failure
/// (e.g. network loss, cloud DB unavailable) that warrants a local-fallback
/// rather than surfacing the error to the caller.
/// </summary>
public interface ITransientFailurePolicy
{
    bool IsTransient(Exception ex);
}
