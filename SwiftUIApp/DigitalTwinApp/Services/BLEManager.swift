import Foundation
import CoreBluetooth

// ===========================================================
// UUIDs — must match the ESP32 firmware exactly
// ===========================================================
struct ESP32UUIDs {
    // ECG Service
    nonisolated(unsafe) static let ecgService        = CBUUID(string: "4fafc201-1fb5-459e-8fcc-c5c9c331914b")
    nonisolated(unsafe) static let ecgCharacteristic = CBUUID(string: "beb5483e-36e1-4688-b7f5-ea07361b26a8")

    // Vitals Service
    nonisolated(unsafe) static let vitalsService      = CBUUID(string: "6e400001-b5a3-f393-e0a9-e50e24dcca9e")
    nonisolated(unsafe) static let hrCharacteristic   = CBUUID(string: "6e400002-b5a3-f393-e0a9-e50e24dcca9e")
    nonisolated(unsafe) static let spo2Characteristic = CBUUID(string: "6e400003-b5a3-f393-e0a9-e50e24dcca9e")
}

// ===========================================================
// BLE Manager — scan, connect, subscribe, decode
// ===========================================================
final class BLEManager: NSObject, ObservableObject, @unchecked Sendable {

    // Published state for SwiftUI views
    @Published var isScanning   = false
    @Published var isConnected  = false
    @Published var deviceName   = ""

    @Published var ecgSamples: [Int] = []     // latest batch of ECG values (centered at 0)
    @Published var heartRate: Float  = 0.0    // BPM with 1 decimal
    @Published var spO2: Float       = 0.0    // % with 1 decimal

    // Ring buffer for ECG display (keeps last ~4 seconds at 250 sps)
    @Published var ecgBuffer: [Int] = []
    private let ecgBufferSize = 1000           // 250 sps * 4 seconds

    // Internal
    private var centralManager: CBCentralManager!
    private var peripheral: CBPeripheral?

    override init() {
        super.init()
        centralManager = CBCentralManager(delegate: self, queue: nil)
    }

    // -------------------------------------------------------
    //  Public API
    // -------------------------------------------------------
    func startScanning() {
        guard centralManager.state == .poweredOn else {
            print("[BLE] Bluetooth not ready, state: \(centralManager.state.rawValue)")
            return
        }
        isScanning = true
        centralManager.scanForPeripherals(
            withServices: [ESP32UUIDs.ecgService],
            options: [CBCentralManagerScanOptionAllowDuplicatesKey: false]
        )
        print("[BLE] Scanning for DigitalTwin-ESP32...")
    }

    func stopScanning() {
        centralManager.stopScan()
        isScanning = false
        print("[BLE] Stopped scanning")
    }

    func disconnect() {
        if let p = peripheral {
            centralManager.cancelPeripheralConnection(p)
        }
    }
}

// ===========================================================
// CBCentralManagerDelegate — scan & connect
// ===========================================================
extension BLEManager: CBCentralManagerDelegate {

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        switch central.state {
        case .poweredOn:
            print("[BLE] Bluetooth powered ON. Waiting for trigger to scan...")
            // startScanning() is now handled by ContentView when the home page mounts
        case .poweredOff:
            print("[BLE] Bluetooth powered OFF")
            isConnected = false
            isScanning = false
        default:
            print("[BLE] Bluetooth state: \(central.state.rawValue)")
        }
    }

    func centralManager(_ central: CBCentralManager,
                        didDiscover peripheral: CBPeripheral,
                        advertisementData: [String: Any],
                        rssi RSSI: NSNumber) {
        let name = peripheral.name ?? "Unknown"
        print("[BLE] Discovered: \(name) (RSSI: \(RSSI))")
        self.peripheral = peripheral
        self.deviceName = name
        stopScanning()
        central.connect(peripheral, options: nil)
        print("[BLE] Connecting to \(name)...")
    }

    func centralManager(_ central: CBCentralManager,
                        didConnect peripheral: CBPeripheral) {
        print("[BLE] Connected to \(peripheral.name ?? "device")")
        isConnected = true
        peripheral.delegate = self
        peripheral.discoverServices([
            ESP32UUIDs.ecgService,
            ESP32UUIDs.vitalsService
        ])
    }

    func centralManager(_ central: CBCentralManager,
                        didDisconnectPeripheral peripheral: CBPeripheral,
                        error: Error?) {
        print("[BLE] Disconnected: \(error?.localizedDescription ?? "clean")")
        isConnected = false
        deviceName  = ""
        startScanning()
    }

    func centralManager(_ central: CBCentralManager,
                        didFailToConnect peripheral: CBPeripheral,
                        error: Error?) {
        print("[BLE] Failed to connect: \(error?.localizedDescription ?? "unknown")")
        startScanning()
    }
}

// ===========================================================
// CBPeripheralDelegate — discover & subscribe
// ===========================================================
extension BLEManager: CBPeripheralDelegate {

    func peripheral(_ peripheral: CBPeripheral,
                    didDiscoverServices error: Error?) {
        guard let services = peripheral.services else { return }
        for service in services {
            print("[BLE] Found service: \(service.uuid)")
            peripheral.discoverCharacteristics(nil, for: service)
        }
    }

    func peripheral(_ peripheral: CBPeripheral,
                    didDiscoverCharacteristicsFor service: CBService,
                    error: Error?) {
        guard let characteristics = service.characteristics else { return }
        for char in characteristics {
            print("[BLE] Found characteristic: \(char.uuid), properties: \(char.properties)")
            if char.properties.contains(.notify) {
                peripheral.setNotifyValue(true, for: char)
                print("[BLE] Subscribed to \(char.uuid)")
            }
            if char.properties.contains(.read) {
                peripheral.readValue(for: char)
            }
        }
    }

    func peripheral(_ peripheral: CBPeripheral,
                    didUpdateValueFor characteristic: CBCharacteristic,
                    error: Error?) {
        guard let data = characteristic.value else { return }

        switch characteristic.uuid {

        // ------ ECG: 10 x uint16 LE samples ------
        case ESP32UUIDs.ecgCharacteristic:
            let samples = decodeECG(data)
            //print("[BLE ECG] \(samples.count) samples: \(samples.prefix(5))...")
            DispatchQueue.main.async {
                self.ecgSamples = samples
                self.ecgBuffer.append(contentsOf: samples)
                if self.ecgBuffer.count > self.ecgBufferSize {
                    self.ecgBuffer.removeFirst(self.ecgBuffer.count - self.ecgBufferSize)
                }
            }

        // ------ Heart Rate: uint16 LE, value * 10 ------
        case ESP32UUIDs.hrCharacteristic:
            let hr = decodeVital(data)
            //print("[BLE HR] \(String(format: "%.1f", hr)) BPM")
            DispatchQueue.main.async { self.heartRate = hr }

        // ------ SpO2: uint16 LE, value * 10 ------
        case ESP32UUIDs.spo2Characteristic:
            let spo2 = decodeVital(data)
            //print("[BLE SpO2] \(String(format: "%.1f", spo2)) %")
            DispatchQueue.main.async { self.spO2 = spo2 }

        default:
            break
        }
    }

    func peripheral(_ peripheral: CBPeripheral,
                    didUpdateNotificationStateFor characteristic: CBCharacteristic,
                    error: Error?) {
        if let error = error {
            print("[BLE] Notify error for \(characteristic.uuid): \(error)")
        } else {
            print("[BLE] Notify \(characteristic.isNotifying ? "ON" : "OFF") for \(characteristic.uuid)")
        }
    }
}

// ===========================================================
// Decoders — match the ESP32 packet format exactly
// ===========================================================
extension BLEManager {

    /// Decode ECG packet: 10 x uint16 little-endian, centered at 0 (subtract 2048 baseline)
    private func decodeECG(_ data: Data) -> [Int] {
        var samples: [Int] = []
        let baseline = 2048
        for i in stride(from: 0, to: data.count - 1, by: 2) {
            let raw = Int(data[i]) | (Int(data[i + 1]) << 8)
            samples.append(raw - baseline)
        }
        return samples
    }

    /// Decode HR or SpO2: uint16 LE, divide by 10 for 1 decimal precision
    private func decodeVital(_ data: Data) -> Float {
        guard data.count >= 2 else { return 0 }
        let raw = UInt16(data[0]) | (UInt16(data[1]) << 8)
        return Float(raw) / 10.0
    }
}
