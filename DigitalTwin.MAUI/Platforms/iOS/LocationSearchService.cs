using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using MapKit;
using ObjCRuntime;

namespace DigitalTwin.Services;

/// <summary>
/// Location search service backed by Apple's on-device MKLocalSearchCompleter.
/// Works fully offline — iOS ships a geographic database as part of the OS.
/// </summary>
public sealed class LocationSearchService : ILocationSearchService, IDisposable
{
    private readonly MKLocalSearchCompleter _completer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // TCS for the current in-flight query; replaced on each new call.
    private TaskCompletionSource<IReadOnlyList<LocationResult>>? _pendingTcs;

    public LocationSearchService()
    {
        _completer = new MKLocalSearchCompleter();
        _completer.Delegate = new CompleterDelegate(this);
    }

    public async Task<IReadOnlyList<LocationResult>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<LocationResult>();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Cancel any in-flight request by completing it empty.
            _pendingTcs?.TrySetResult(Array.Empty<LocationResult>());

            var tcs = new TaskCompletionSource<IReadOnlyList<LocationResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingTcs = tcs;

            // Set the query on the main thread (MapKit requirement).
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _completer.QueryFragment = query;
            });

            // Wait for delegate callback, with a 3-second safety timeout.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<LocationResult>();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void OnResultsUpdated(IReadOnlyList<LocationResult> results)
    {
        _pendingTcs?.TrySetResult(results);
    }

    internal void OnError(NSError error)
    {
        _pendingTcs?.TrySetResult(Array.Empty<LocationResult>());
    }

    public void Dispose()
    {
        _pendingTcs?.TrySetCanceled();
        _completer.Delegate = null;
        _completer.Dispose();
        _gate.Dispose();
    }

    // ── Inner delegate ───────────────────────────────────────────────────────

    private sealed class CompleterDelegate : MKLocalSearchCompleterDelegate
    {
        private readonly LocationSearchService _owner;

        public CompleterDelegate(LocationSearchService owner)
        {
            _owner = owner;
        }

        // Use [Export] to bind directly to the Objective-C selector,
        // bypassing C# virtual method name resolution in the binding layer.
        [Export("completerDidUpdateResults:")]
        public new void DidUpdateResults(MKLocalSearchCompleter completer)
        {
            var results = new List<LocationResult>(Math.Min(completer.Results.Length, 8));

            foreach (var completion in completer.Results)
            {
                // Title    = place/street name   e.g. "Calea Cristești, 35"  or "Iași"
                // Subtitle = broader context     e.g. "Holboca, Romania"     or "Romania"
                var title    = completion.Title ?? string.Empty;
                var subtitle = completion.Subtitle ?? string.Empty;

                var country = string.Empty;
                var city    = title; // fallback: title is the place name (e.g. pure city result)

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    var parts = subtitle.Split(',');
                    country = parts[^1].Trim();

                    if (parts.Length == 1)
                    {
                        // Subtitle is just "<Country>" — the title IS the city/place.
                        city = title;
                    }
                    else if (parts.Length == 2)
                    {
                        // "<City>, <Country>" — straightforward.
                        city = parts[0].Trim();
                    }
                    else
                    {
                        // 3+ segments: "Street, Number, City, PostalCode, Country"
                        // Walk backwards (skipping country) to find the first non-numeric segment.
                        city = title; // keep title as fallback
                        for (var i = parts.Length - 2; i >= 0; i--)
                        {
                            var seg = parts[i].Trim();
                            if (!string.IsNullOrWhiteSpace(seg) && !seg.All(char.IsDigit))
                            {
                                city = seg;
                                break;
                            }
                        }
                    }
                }

                var display = string.IsNullOrWhiteSpace(subtitle)
                    ? title
                    : $"{title}, {subtitle}";

                results.Add(new LocationResult(display, city, country));

                if (results.Count >= 8) break;
            }

            _owner.OnResultsUpdated(results);
        }

        [Export("completer:didFailWithError:")]
        public new void DidFail(MKLocalSearchCompleter completer, NSError error)
        {
            _owner.OnError(error);
        }
    }
}
