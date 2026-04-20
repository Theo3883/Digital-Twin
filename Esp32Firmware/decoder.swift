
import Foundation
import CoreBluetooth
import Combine

// ===========================================================
//  UUIDs — must match the ESP32 firmware exactly
// ===========================================================
struct ESP32UUIDs {
    // ECG Service
    static let ecgService        = CBUUID(string: "4fafc201-1fb5-459e-8fcc-c5c9c331914b")
    static let ecgCharacteristic = CBUUID(string: "beb5483e-36e1-4688-b7f5-ea07361b26a8")

    // Vitals Service
    static let vitalsService        = CBUUID(string: "6e400001-b5a3-f393-e0a9-e50e24dcca9e")
    static let hrCharacteristic     = CBUUID(string: "6e400002-b5a3-f393-e0a9-e50e24dcca9e")
    static let spo2Characteristic   = CBUUID(string: "6e400003-b5a3-f393-e0a9-e50e24dcca9e")
}

// ===========================================================
//  BLE Manager — scan, connect, subscribe, decode
// ===========================================================
class BLEManager: NSObject, ObservableObject {

    // Published state for SwiftUI views
    @Published var isScanning       = false
    @Published var isConnected      = false
    @Published var deviceName       = ""

    @Published var ecgSamples: [[Int]] = Array(repeating: [], count: 12)  // 12-lead latest batch
    @Published var heartRate: Float  = 0.0     // BPM with 1 decimal
    @Published var spO2: Float       = 0.0     // % with 1 decimal

    // Internal
    private var centralManager: CBCentralManager!
    private var peripheral: CBPeripheral?

    // 12-lead ring buffer for ML inference [12][4096] matches CoreML (1,4096,12)
    @Published var ecgBuffer: [[Int]] = Array(repeating: [], count: 12)
    private let ecgBufferSize = 4096

    /// True when all 12 leads have 4096 samples (ready for ML inference)
    var isBufferReady: Bool { ecgBuffer.allSatisfy { $0.count >= ecgBufferSize } }

    /// Get the buffer as a flat [4096][12] array for CoreML input
    func getMLInput() -> [[Int]] {
        guard isBufferReady else { return [] }
        // Transpose from [12][4096] to [4096][12]
        return (0..<ecgBufferSize).map { t in
            (0..<12).map { lead in ecgBuffer[lead][t] }
        }
    }

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
        // Scan for devices advertising the ECG service
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
//  CBCentralManagerDelegate — scan & connect
// ===========================================================
extension BLEManager: CBCentralManagerDelegate {

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        switch central.state {
        case .poweredOn:
            print("[BLE] Bluetooth powered ON")
            startScanning()
        case .poweredOff:
            print("[BLE] Bluetooth powered OFF")
            isConnected = false
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

        // Connect to the first device that advertises our service
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
        // Discover both services
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
        deviceName = ""
        // Auto-reconnect
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
//  CBPeripheralDelegate — discover & subscribe
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

            // Subscribe to all notify characteristics
            if char.properties.contains(.notify) {
                peripheral.setNotifyValue(true, for: char)
                print("[BLE] Subscribed to \(char.uuid)")
            }

            // Read initial value of readable characteristics
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

        // ------ ECG: 5 timesteps x 12 leads x uint16 LE = 120 bytes ------
        case ESP32UUIDs.ecgCharacteristic:
            let decoded = decode12LeadECG(data)
            DispatchQueue.main.async {
                self.ecgSamples = decoded
                // Append each lead's samples to its buffer
                for lead in 0..<12 {
                    self.ecgBuffer[lead].append(contentsOf: decoded[lead])
                    if self.ecgBuffer[lead].count > self.ecgBufferSize {
                        self.ecgBuffer[lead].removeFirst(
                            self.ecgBuffer[lead].count - self.ecgBufferSize
                        )
                    }
                }
            }

        // ------ Heart Rate: uint16 LE, value * 10 ------
        case ESP32UUIDs.hrCharacteristic:
            let hr = decodeVital(data)
            DispatchQueue.main.async {
                self.heartRate = hr
            }

        // ------ SpO2: uint16 LE, value * 10 ------
        case ESP32UUIDs.spo2Characteristic:
            let spo2 = decodeVital(data)
            DispatchQueue.main.async {
                self.spO2 = spo2
            }

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
//  Decoders — match the ESP32 packet format exactly
// ===========================================================
extension BLEManager {

    /// Decode 12-lead ECG packet: 5 timesteps x 12 leads x uint16 LE = 120 bytes
    /// Returns [[Int]] with shape [12][5] — 5 samples per lead, centered at 0
    private func decode12LeadECG(_ data: Data) -> [[Int]] {
        let numLeads = 12
        let baseline = 2048
        let samplesPerPacket = 5
        var result: [[Int]] = Array(repeating: [], count: numLeads)

        // Packet layout: [t0_I, t0_II, ..., t0_V6, t1_I, t1_II, ..., t4_V6]
        var offset = 0
        for _ in 0..<samplesPerPacket {
            for lead in 0..<numLeads {
                guard offset + 1 < data.count else { break }
                let raw = Int(data[offset]) | (Int(data[offset + 1]) << 8)
                result[lead].append(raw - baseline)
                offset += 2
            }
        }
        return result
    }

    /// Decode HR or SpO2: uint16 LE, divide by 10 for 1 decimal precision
    private func decodeVital(_ data: Data) -> Float {
        guard data.count >= 2 else { return 0 }
        let raw = UInt16(data[0]) | (UInt16(data[1]) << 8)
        return Float(raw) / 10.0
    }
}
