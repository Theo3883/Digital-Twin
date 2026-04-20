import Foundation
import CoreBluetooth
import CoreML


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
// Packet format: 5 timesteps × 12 leads × uint16 LE = 120 bytes
// Lead order: I, II, III, aVR, aVL, aVF, V1–V6
// ===========================================================
final class BLEManager: NSObject, ObservableObject, @unchecked Sendable {

    // Published state for SwiftUI views
    @Published var isScanning   = false
    @Published var isConnected  = false
    @Published var deviceName   = ""

    /// Latest decoded batch — shape [12][5]: 5 samples per lead per packet
    @Published var ecgSamples: [[Int]] = Array(repeating: [], count: 12)

    @Published var heartRate: Float = 0.0   // BPM with 1 decimal
    @Published var spO2: Float      = 0.0   // % with 1 decimal

    // ── 12-lead ring buffer [12][1000] ─────────────────────────────────────
    // Buffer 10 seconds of data at 100 Hz = 1000 samples
    @Published var ecgBuffer: [[Int]] = Array(repeating: [], count: 12)
    private let ecgBufferSize = 1000

    /// True when all 12 leads have ≥ 1000 samples — CoreML inference is ready.
    var isBufferReady: Bool {
        ecgBuffer.allSatisfy { $0.count >= ecgBufferSize }
    }

    /// Returns the buffer shaped as [12][1000] for PTB-XL Model.
    func getMLInput() -> [[Int]] {
        guard isBufferReady else { return [] }
        // The ESP32 is natively streaming 100Hz, simply slice the 1000 frame window.
        return (0..<12).map { lead in Array(ecgBuffer[lead].suffix(ecgBufferSize)) }
    }

    /// Convenience: Lead II (index 1) as a flat array for the waveform display.
    var leadIIBuffer: [Int] { ecgBuffer.count > 1 ? ecgBuffer[1] : [] }

    // Internal
    private var centralManager: CBCentralManager!
    private var peripheral: CBPeripheral?

    override init() {
        super.init()
        centralManager = CBCentralManager(delegate: self, queue: nil)
    }

    // Clear buffer on disconnect so next connection starts fresh
    func clearBuffer() {
        ecgBuffer = Array(repeating: [], count: 12)
        ecgSamples = Array(repeating: [], count: 12)
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
            // startScanning() is called by ContentView when the home page mounts
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
        clearBuffer()
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

        // ------ ECG: 5 timesteps × 12 leads × uint16 LE = 120 bytes ------
        case ESP32UUIDs.ecgCharacteristic:
            let decoded = decode12LeadECG(data)
            DispatchQueue.main.async {
                self.ecgSamples = decoded
                // Append each lead's 5 samples to its ring buffer
                for lead in 0..<12 {
                    self.ecgBuffer[lead].append(contentsOf: decoded[lead])
                    if self.ecgBuffer[lead].count > self.ecgBufferSize {
                        self.ecgBuffer[lead].removeFirst(
                            self.ecgBuffer[lead].count - self.ecgBufferSize
                        )
                    }
                }
            }

        // ------ Heart Rate: uint16 LE, value × 10 ------
        case ESP32UUIDs.hrCharacteristic:
            let hr = decodeVital(data)
            DispatchQueue.main.async { self.heartRate = hr }

        // ------ SpO2: uint16 LE, value × 10 ------
        case ESP32UUIDs.spo2Characteristic:
            let spo2 = decodeVital(data)
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
// Decoders — match the ESP32 firmware packet format exactly
// ===========================================================
extension BLEManager {

    /// Decode 12-lead ECG packet: 5 timesteps × 12 leads × uint16 LE = 120 bytes
    /// Packet layout within each timestep: [t_I, t_II, ..., t_V6]
    /// Returns [[Int]] with shape [12][5] — 5 samples per lead, centred at 0
    private func decode12LeadECG(_ data: Data) -> [[Int]] {
        let numLeads       = 12
        let samplesPerPkt  = 5
        let baseline       = 2048
        var result: [[Int]] = Array(repeating: [], count: numLeads)

        var offset = 0
        for _ in 0..<samplesPerPkt {
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

// ===========================================================
//  ECG Classification Result
// ===========================================================

struct ECGClassification {
    /// Probabilities for each of the 6 Ribeiro model classes (0.0–1.0)
    let probabilities: [String: Double]

    /// Highest-probability label, localised to a clinical term
    let topLabel: String

    /// Confidence of the top prediction (0.0–1.0)
    let topConfidence: Double

    /// True when any class probability exceeds the abnormality threshold (0.5)
    let isAbnormal: Bool

    /// Human-readable summary: "AF (87%)" or "Normal Sinus Rhythm (94%)"
    var summary: String {
        "\(topLabel) (\(Int(topConfidence * 100))%)"
    }
}

// ===========================================================
//  PTB-XL Classifier Service
//  Wraps PTBXLClassifier.mlmodelc (xresnet1d101 PTB-XL)
//  Input:  MLMultiArray(shape: [1, 12, 1000], dataType: .float32)
//  Output: 71-element probability vector
// ===========================================================

final class PTBXLClassifierService {

    private static let abnormalityThreshold: Double = 0.5

    // ESP32 ADC scale conversion
    private static let adcToVoltScale: Float = 1e-3

    private var model: MLModel?
    private(set) var isLoaded = false

    // MARK: - Loading

    /// Load the PTBXLClassifier.mlmodelc from the main bundle.
    func load() throws {
        guard !isLoaded else { return }

        guard let modelURL = Bundle.main.url(
            forResource: "PTBXLClassifier",
            withExtension: "mlmodelc"
        ) else {
            throw ECGClassifierError.modelNotFound
        }

        let config = MLModelConfiguration()

        config.computeUnits = .cpuAndNeuralEngine
        model = try MLModel(contentsOf: modelURL, configuration: config)
        isLoaded = true
        print("[PTBXLClassifier] Model loaded successfully from \(modelURL.lastPathComponent)")
    }

    // MARK: - Inference

    /// Classify a full 12-lead window.
    /// - Parameter mlInput: [12][1000] integer array from BLEManager.getMLInput()
    /// - Returns: ECGClassification or nil if model not loaded / inference fails
    func classify(mlInput: [[Int]]) -> ECGClassification? {
        guard let model, isLoaded else {
            print("[PTBXLClassifier] Model not loaded — skipping inference")
            return nil
        }
        guard mlInput.count == 12, mlInput.first?.count == 1000 else {
            print("[PTBXLClassifier] Input shape mismatch: expected [12][1000], got [\(mlInput.count)][\(mlInput.first?.count ?? 0)]")
            return nil
        }

        do {
            // Build MLMultiArray shape (1, 12, 1000)
            let array = try MLMultiArray(shape: [1, 12, 1000], dataType: .float32)
            for lead in 0..<12 {
                for t in 0..<1000 {
                    let idx = [0, lead, t] as [NSNumber]
                    array[idx] = NSNumber(value: Float(mlInput[lead][t]) * Self.adcToVoltScale)
                }
            }

            let input  = try MLDictionaryFeatureProvider(dictionary: ["ecg_signal": array])
            let output = try model.prediction(from: input)

            return extractClassification(from: output)

        } catch {
            print("[PTBXLClassifier] Inference error: \(error.localizedDescription)")
            return nil
        }
    }

    // MARK: - Private helpers

    private func extractClassification(from output: MLFeatureProvider) -> ECGClassification? {
        // The output feature name may vary; try common names from CoreML conversion
        let featureNames = output.featureNames
        guard let outputName = featureNames.first,
              let multiArray = output.featureValue(for: outputName)?.multiArrayValue
        else {
            print("[PTBXLClassifier] Could not read output feature. Available: \(output.featureNames)")
            return nil
        }

        var probs: [String: Double] = [:]
        for i in 0..<multiArray.count {
            // PTB-XL PyTorch wrapper outputs raw logits natively.
            // We must apply the Sigmoid activation mathematically: 1 / (1 + e^-x)
            let logit = multiArray[i].doubleValue
            let probability = 1.0 / (1.0 + exp(-logit))
            
            // Dynamically assign array index as class label
            probs["Class \(i)"] = probability
        }

        // Find the highest-confidence label
        let sorted = probs.sorted { $0.value > $1.value }
        guard let top = sorted.first else { return nil }

        let isAbnormal  = top.value > Self.abnormalityThreshold
        let topLabel    = isAbnormal ? top.key : ("Normal Sinus Rhythm")

        return ECGClassification(
            probabilities:  probs,
            topLabel:       topLabel,
            topConfidence:  top.value,
            isAbnormal:     isAbnormal
        )
    }
}

// MARK: - Errors

enum ECGClassifierError: LocalizedError {
    case modelNotFound
    case inputShapeMismatch

    var errorDescription: String? {
        switch self {
        case .modelNotFound:        return "PTBXLClassifier.mlmodelc not found in bundle. Ensure the compiled ML model is packaged."
        case .inputShapeMismatch:   return "ECG input array must be [1, 12, 1000]"
        }
    }
}
