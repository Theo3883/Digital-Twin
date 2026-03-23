using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Integrations.Ecg;

/// <summary>
/// Connects to the ESP32 WebSocket server and streams ECG frames to subscribers.
/// The ESP32 acts as a WebSocket server; this client connects to it on the local network.
/// Expected payload: { "ecg": [int16...], "spo2": float, "hr": int, "ts": long }
/// </summary>
public class EcgStreamClient : IEcgStreamProvider, IAsyncDisposable
{
    private readonly Subject<EcgFrame> _frames = new();
    private readonly ILogger<EcgStreamClient> _logger;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;

    public EcgStreamClient(ILogger<EcgStreamClient> logger)
    {
        _logger = logger;
    }

    public IObservable<EcgFrame> GetEcgStream() => _frames;

    public async Task ConnectAsync(string deviceUrl, CancellationToken cancellationToken = default)
    {
        if (_webSocket is { State: WebSocketState.Open })
        {
            _logger.LogWarning("EcgStreamClient already connected.");
            return;
        }

        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Connecting to ESP32 at {Url}", deviceUrl);

        await _webSocket.ConnectAsync(new Uri(deviceUrl), cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Connected to ESP32 at {Url}", deviceUrl);

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoop = ReceiveLoopAsync(_receiveCts.Token);
    }

    public async Task DisconnectAsync()
    {
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_receiveLoop is not null)
        {
            try { await _receiveLoop; }
            catch (OperationCanceledException) { /* expected on cancellation */ }
            _receiveLoop = null;
        }

        if (_webSocket is { State: WebSocketState.Open })
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing WebSocket.");
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("ESP32 closed the WebSocket connection.");
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var frame = ParseFrame(json);

                if (frame is not null)
                    _frames.OnNext(frame);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving ECG frame.");
            }
        }
    }

    private EcgFrame? ParseFrame(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var samples = root.TryGetProperty("ecg", out var ecgProp)
                ? ecgProp.EnumerateArray().Select(e => (double)e.GetInt16()).ToArray()
                : [];

            var spO2 = root.TryGetProperty("spo2", out var spo2Prop) ? spo2Prop.GetDouble() : 0;
            var hr = root.TryGetProperty("hr", out var hrProp) ? hrProp.GetInt32() : 0;
            var ts = root.TryGetProperty("ts", out var tsProp)
                ? DateTimeOffset.FromUnixTimeMilliseconds(tsProp.GetInt64()).UtcDateTime
                : DateTime.UtcNow;

            return new EcgFrame
            {
                Samples = samples,
                SpO2 = spO2,
                HeartRate = hr,
                Timestamp = ts
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse ECG frame: {Json}", json);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _frames.OnCompleted();
        _frames.Dispose();
        GC.SuppressFinalize(this);
    }
}
