using System.Net.Sockets;
using DigitalTwin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Infrastructure.Services;

/// <summary>
/// Singleton circuit-breaker that probes the PostgreSQL host with a lightweight
/// TCP connect (≤1 s) and caches the result so all callers can skip cloud queries
/// immediately when the DB is known to be unreachable.
///
/// Cache TTLs:
///   Available   → 30 s (re-probe after to confirm still up)
///   Unavailable → 15 s (re-probe sooner so recovery is detected quickly)
/// </summary>
public sealed class CloudHealthService : ICloudHealthService
{
    private static readonly TimeSpan ProbeTimeout      = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AvailableTtl      = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnavailableTtl    = TimeSpan.FromSeconds(15);

    private readonly string _host;
    private readonly int    _port;
    private readonly ILogger<CloudHealthService> _logger;

    // ── Circuit-breaker state ────────────────────────────────────────────────
    // Optimistic default: assume available on first access so the first call
    // performs a real probe instead of blocking a caller with a cold start.
    private volatile bool _isAvailable  = true;
    private long          _lastProbeTicks = DateTime.MinValue.Ticks;    // Interlocked-safe
    private readonly SemaphoreSlim _probeLock = new(1, 1);

    public CloudHealthService(string host, int port, ILogger<CloudHealthService> logger)
    {
        _host   = host;
        _port   = port;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync()
    {
        var now = DateTime.UtcNow;
        var ttl = _isAvailable ? AvailableTtl : UnavailableTtl;

        // Return cached result while TTL is still valid.
        var lastProbe = new DateTime(Interlocked.Read(ref _lastProbeTicks), DateTimeKind.Utc);
        if (now - lastProbe < ttl)
            return _isAvailable;

        // TTL expired — we need a fresh probe.
        // Non-blocking TryWait: if another thread is already probing, just return
        // the last known state rather than queuing callers behind the probe.
        if (!await _probeLock.WaitAsync(0))
            return _isAvailable;

        try
        {
            // Double-check after acquiring the lock (another thread may have just probed).
            now      = DateTime.UtcNow;
            lastProbe = new DateTime(Interlocked.Read(ref _lastProbeTicks), DateTimeKind.Utc);
            ttl      = _isAvailable ? AvailableTtl : UnavailableTtl;
            if (now - lastProbe < ttl)
                return _isAvailable;

            var reachable = await TcpProbeAsync();
            _isAvailable = reachable;
            Interlocked.Exchange(ref _lastProbeTicks, DateTime.UtcNow.Ticks);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[CloudHealth] Probe {Host}:{Port} → {State}.", _host, _port, reachable ? "AVAILABLE" : "UNAVAILABLE");

            return reachable;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    /// <inheritdoc />
    public void ReportFailure()
    {
        _isAvailable = false;
        Interlocked.Exchange(ref _lastProbeTicks, DateTime.UtcNow.Ticks);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[CloudHealth] Failure reported — cloud marked unavailable for {Ttl}s.", UnavailableTtl.TotalSeconds);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<bool> TcpProbeAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(ProbeTimeout);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_host, _port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
