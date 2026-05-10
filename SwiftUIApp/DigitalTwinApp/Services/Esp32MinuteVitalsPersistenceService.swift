import Combine
import Foundation

@MainActor
final class Esp32MinuteVitalsPersistenceService: ObservableObject {
    static let shared = Esp32MinuteVitalsPersistenceService()
    static let averageSource = "ESP32_1M_AVG"

    @Published private(set) var isBleConnected = false
    @Published private(set) var latestLiveHeartRate: Double = 0
    @Published private(set) var latestLiveSpO2: Double = 0
    @Published private(set) var latestMinuteAverageHeartRate: Double = 0
    @Published private(set) var latestMinuteAverageSpO2: Double = 0
    @Published private(set) var lastPersistedAt: Date?

    private weak var bleManager: BLEManager?
    private weak var engine: MobileEngineWrapper?

    private var subscriptions = Set<AnyCancellable>()
    private var sampleTimer: Timer?

    private var bucketStart: Date?
    private var heartRateSamples: [Double] = []
    private var spO2Samples: [Double] = []

    private let minimumSamplesToPersist = 2

    private init() {}

    func attach(bleManager: BLEManager, engine: MobileEngineWrapper) {
        self.bleManager = bleManager
        self.engine = engine

        if subscriptions.isEmpty {
            bleManager.$isConnected
                .receive(on: RunLoop.main)
                .sink { [weak self] connected in
                    guard let self else { return }
                    Task { @MainActor in
                        self.handleConnectionState(connected)
                    }
                }
                .store(in: &subscriptions)
        }

        handleConnectionState(bleManager.isConnected)
    }

    private func handleConnectionState(_ connected: Bool) {
        isBleConnected = connected

        if connected {
            bucketStart = nil
            heartRateSamples.removeAll(keepingCapacity: true)
            spO2Samples.removeAll(keepingCapacity: true)
            startSampleTimer()
            return
        }

        stopSampleTimer()
        flushBucket(triggeredAt: Date())
        latestLiveHeartRate = 0
        latestLiveSpO2 = 0
    }

    private func startSampleTimer() {
        stopSampleTimer()

        sampleTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            guard let self else { return }
            Task { @MainActor in
                self.collectCurrentSample()
            }
        }

        if let timer = sampleTimer {
            RunLoop.main.add(timer, forMode: .common)
        }
    }

    private func stopSampleTimer() {
        sampleTimer?.invalidate()
        sampleTimer = nil
    }

    private func collectCurrentSample() {
        guard let ble = bleManager, ble.isConnected else {
            return
        }

        let now = Date()
        if bucketStart == nil {
            bucketStart = now
        }

        let heartRate = Double(ble.heartRate)
        let spO2 = Double(ble.spO2)
        print("[ESP32Vitals] Raw BLE heartRate=\(heartRate) spO2=\(spO2)")
        
        if isValidHeartRate(heartRate) {
            latestLiveHeartRate = heartRate
            heartRateSamples.append(heartRate)
        }

        if isValidSpO2(spO2) {
            latestLiveSpO2 = spO2
            spO2Samples.append(spO2)
        }

        if let start = bucketStart, now.timeIntervalSince(start) >= 5 {
            print("[ESP32Vitals] Flushing bucket: HR samples=\(heartRateSamples.count) SpO2 samples=\(spO2Samples.count)")
            flushBucket(triggeredAt: now)
        }
    }

    private func flushBucket(triggeredAt: Date) {
        let heartRateAverage = heartRateSamples.count >= minimumSamplesToPersist
            ? average(heartRateSamples)
            : nil
        let spO2Average = spO2Samples.count >= minimumSamplesToPersist
            ? average(spO2Samples)
            : nil

        heartRateSamples.removeAll(keepingCapacity: true)
        spO2Samples.removeAll(keepingCapacity: true)
        bucketStart = triggeredAt

        guard heartRateAverage != nil || spO2Average != nil else {
            return
        }

        if let heartRateAverage {
            latestMinuteAverageHeartRate = heartRateAverage
        }

        if let spO2Average {
            latestMinuteAverageSpO2 = spO2Average
        }

        Task { [weak self] in
            guard let self else { return }
            await self.persistMinuteAverages(
                heartRateAverage: heartRateAverage,
                spO2Average: spO2Average,
                timestamp: triggeredAt)
        }
    }

    private func persistMinuteAverages(
        heartRateAverage: Double?,
        spO2Average: Double?,
        timestamp: Date
    ) async {
        guard let engine else {
            print("[ESP32Vitals] ⚠️ engine is nil — vitals dropped!")
            return
        }
        print("[ESP32Vitals] Persisting HR=\(heartRateAverage ?? -1) SpO2=\(spO2Average ?? -1)")

        var wroteAnyAverage = false

        if let heartRateAverage {
            let heartRateInput = VitalSignInput(
                type: .heartRate,
                value: heartRateAverage,
                unit: VitalSignType.heartRate.unit,
                source: Self.averageSource,
                timestamp: timestamp)

            wroteAnyAverage = await engine.recordVitalSign(heartRateInput) || wroteAnyAverage
        }

        if let spO2Average {
            let spO2Input = VitalSignInput(
                type: .spO2,
                value: spO2Average,
                unit: VitalSignType.spO2.unit,
                source: Self.averageSource,
                timestamp: timestamp)

            wroteAnyAverage = await engine.recordVitalSign(spO2Input) || wroteAnyAverage
        }

        guard wroteAnyAverage else {
            return
        }

        lastPersistedAt = timestamp

        if engine.isCloudAuthenticated {
            _ = await engine.pushLocalChanges()
        }
    }

    private func isValidHeartRate(_ value: Double) -> Bool {
        value >= 20 && value <= 300
    }

    private func isValidSpO2(_ value: Double) -> Bool {
        value >= 70 && value <= 100
    }

    private func average(_ values: [Double]) -> Double {
        guard !values.isEmpty else { return 0 }
        return values.reduce(0, +) / Double(values.count)
    }
}
