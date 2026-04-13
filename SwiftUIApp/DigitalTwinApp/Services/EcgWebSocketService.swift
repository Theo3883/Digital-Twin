import Foundation

/// Represents a parsed ECG frame from the ESP32 device
struct EcgDeviceFrame: Sendable {
    let samples: [Double]
    let heartRate: Double
    let spO2: Double
    let timestamp: Date
}

/// Protocol for ECG device connections (legacy WiFi interface, kept for compatibility)
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

// EcgWebSocketService (WiFi/WebSocket) has been replaced by BLEManager (Bluetooth).
// See Services/BLEManager.swift.
