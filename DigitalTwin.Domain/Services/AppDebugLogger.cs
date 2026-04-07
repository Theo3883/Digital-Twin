using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Dual-channel logger wrapper that guards against expensive argument evaluation
/// when a given log level is disabled.
///
/// <list type="bullet">
///   <item>Channel 1 — <see cref="ILogger{T}"/> (structured, level-filtered).</item>
///   <item>Channel 2 — <see cref="System.Diagnostics.Debug.WriteLine"/> (IDE output, DEBUG builds only).</item>
/// </list>
///
/// Inject <c>AppDebugLogger&lt;T&gt;</c> instead of <c>ILogger&lt;T&gt;</c> in service
/// constructors.  The open-generic registration in DI resolves <c>ILogger&lt;T&gt;</c>
/// automatically.
/// </summary>
public sealed class AppDebugLogger<T>
{
    private readonly ILogger<T> _inner;

    public AppDebugLogger(ILogger<T> inner) => _inner = inner;

    // ── Convenience: expose the inner logger for edge cases ──────────────────

    /// <summary>
    /// The underlying <see cref="ILogger{T}"/> instance, for scenarios that need
    /// the raw logger (e.g. passing to a library that requires <c>ILogger</c>).
    /// </summary>
    public ILogger<T> Inner => _inner;

    // ── Level-guarded methods ────────────────────────────────────────────────

    public void Debug(string messageTemplate, params object[] args)
    {
        if (!_inner.IsEnabled(LogLevel.Debug)) return;
        _inner.LogDebug(messageTemplate, args);
        WriteDebugLine(messageTemplate, args);
    }

    public void Debug(Exception ex, string messageTemplate, params object[] args)
    {
        if (!_inner.IsEnabled(LogLevel.Debug)) return;
        _inner.LogDebug(ex, messageTemplate, args);
        WriteDebugLine(messageTemplate, args);
    }

    public void Info(string messageTemplate, params object[] args)
    {
        if (!_inner.IsEnabled(LogLevel.Information)) return;
        _inner.LogInformation(messageTemplate, args);
        WriteDebugLine(messageTemplate, args);
    }

    public void Warn(string messageTemplate, params object[] args)
    {
        if (!_inner.IsEnabled(LogLevel.Warning)) return;
        _inner.LogWarning(messageTemplate, args);
        WriteDebugLine(messageTemplate, args);
    }

    public void Warn(Exception ex, string messageTemplate, params object[] args)
    {
        if (!_inner.IsEnabled(LogLevel.Warning)) return;
        _inner.LogWarning(ex, messageTemplate, args);
        WriteDebugLine(messageTemplate, args);
    }

    public void Error(Exception ex, string messageTemplate, params object[] args)
    {
        if (!_inner.IsEnabled(LogLevel.Error)) return;
        _inner.LogError(ex, messageTemplate, args);
        WriteDebugLine(messageTemplate, args);
    }

    // ── IDE debug output (DEBUG builds only) ─────────────────────────────────

    [Conditional("DEBUG")]
    private static void WriteDebugLine(string template, object[] args)
    {
        if (args.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine(template);
            return;
        }

        // Structured log templates use named placeholders ({Name}) not positional ({0}).
        // Convert to a simple readable string for the IDE debug console.
        try
        {
            var index = 0;
            var formatted = System.Text.RegularExpressions.Regex.Replace(
                template, @"\{[^}]+\}", _ => index < args.Length ? args[index++]?.ToString() ?? "null" : "?");
            System.Diagnostics.Debug.WriteLine(formatted);
        }
        catch
        {
            // Formatting failure should never crash the app.
            System.Diagnostics.Debug.WriteLine(template);
        }
    }
}
