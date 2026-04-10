import Foundation

/// Represents a parsed ECG frame from the ESP32 device
struct EcgDeviceFrame: Sendable {
    let samples: [Double]
    let heartRate: Double
    let spO2: Double
    let timestamp: Date
}

/// Protocol for ECG device connections
protocol EcgDeviceConnection: Sendable {
    func connect(url: URL) async
    func disconnect() async
    var onFrame: (@Sendable (EcgDeviceFrame) -> Void)? { get set }
    var onStateChange: (@Sendable (EcgConnectionState) -> Void)? { get set }
}

enum EcgConnectionState: Sendable {
    case disconnected
    case connecting
    case connected
    case reconnecting(attempt: Int)
    case error(String)
}

/// WebSocket service for real ESP32 ECG device connection
actor EcgWebSocketService: EcgDeviceConnection {
    private var webSocketTask: URLSessionWebSocketTask?
    private let session = URLSession(configuration: .default)
    private var isActive = false
    private var reconnectAttempts = 0
    private let maxReconnectAttempts = 5

    nonisolated(unsafe) var onFrame: (@Sendable (EcgDeviceFrame) -> Void)?
    nonisolated(unsafe) var onStateChange: (@Sendable (EcgConnectionState) -> Void)?

    func connect(url: URL) async {
        guard !isActive else { return }
        isActive = true
        reconnectAttempts = 0
        await performConnect(url: url)
    }

    func disconnect() async {
        isActive = false
        webSocketTask?.cancel(with: .goingAway, reason: nil)
        webSocketTask = nil
        onStateChange?(.disconnected)
    }

    private func performConnect(url: URL) async {
        onStateChange?(.connecting)

        let task = session.webSocketTask(with: url)
        self.webSocketTask = task
        task.resume()

        onStateChange?(.connected)
        reconnectAttempts = 0

        await receiveMessages(url: url)
    }

    private func receiveMessages(url: URL) async {
        guard let task = webSocketTask else { return }

        while isActive {
            do {
                let message = try await task.receive()
                switch message {
                case .string(let text):
                    if let frame = parseFrame(text) {
                        onFrame?(frame)
                    }
                case .data(let data):
                    if let text = String(data: data, encoding: .utf8),
                       let frame = parseFrame(text) {
                        onFrame?(frame)
                    }
                @unknown default:
                    break
                }
            } catch {
                if isActive {
                    await attemptReconnect(url: url)
                }
                return
            }
        }
    }

    private func attemptReconnect(url: URL) async {
        guard isActive, reconnectAttempts < maxReconnectAttempts else {
            onStateChange?(.error("Connection lost after \(reconnectAttempts) attempts"))
            isActive = false
            return
        }

        reconnectAttempts += 1
        onStateChange?(.reconnecting(attempt: reconnectAttempts))

        // Exponential backoff: 1s, 2s, 4s, 8s, 16s
        let delay = UInt64(pow(2.0, Double(reconnectAttempts - 1))) * 1_000_000_000
        try? await Task.sleep(nanoseconds: delay)

        if isActive {
            await performConnect(url: url)
        }
    }

    private func parseFrame(_ json: String) -> EcgDeviceFrame? {
        guard let data = json.data(using: .utf8) else { return nil }

        struct RawFrame: Codable {
            let samples: [Double]?
            let heartRate: Double?
            let spO2: Double?
            let hr: Double?
            let spo2: Double?
        }

        guard let raw = try? JSONDecoder().decode(RawFrame.self, from: data) else { return nil }
        let samples = raw.samples ?? []
        guard !samples.isEmpty else { return nil }

        return EcgDeviceFrame(
            samples: samples,
            heartRate: raw.heartRate ?? raw.hr ?? 0,
            spO2: raw.spO2 ?? raw.spo2 ?? 0,
            timestamp: Date()
        )
    }
}
