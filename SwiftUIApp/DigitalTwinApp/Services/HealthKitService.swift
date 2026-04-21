import HealthKit
import Foundation

/// Service for integrating with HealthKit to read and write health data
@MainActor
class HealthKitService: ObservableObject {
    
    // MARK: - Properties
    
    private let healthStore = HKHealthStore()
    @Published var isAuthorized = false
    @Published var authorizationStatus: HKAuthorizationStatus = .notDetermined

    #if DEBUG
    private let isDebugLoggingEnabled = true
    #else
    private let isDebugLoggingEnabled = false
    #endif

    private func hkDebug(_ message: String) {
        guard isDebugLoggingEnabled else { return }
        print("[HealthKitService][Debug] \(message)")
    }

    var sleepAuthorizationStatus: HKAuthorizationStatus {
        // NOTE: HealthKit does NOT allow querying READ authorization status.
        // This will only return status for WRITE permissions.
        // Since we don't write sleep data, this is informational only and should not block reads.
        guard let sleepType = HKObjectType.categoryType(forIdentifier: .sleepAnalysis) else {
            return .notDetermined
        }

        return healthStore.authorizationStatus(for: sleepType)
    }

    func getAuthStatusDescription(_ status: HKAuthorizationStatus) -> String {
        switch status {
        case .notDetermined: return "notDetermined (0) - System prompt needed"
        case .sharingDenied: return "sharingDenied (1) - User explicitly denied access in Health App"
        case .sharingAuthorized: return "sharingAuthorized (2) - Access granted"
        @unknown default: return "unknown (\(status.rawValue))"
        }
    }
    
    // MARK: - HealthKit Data Types
    
    /// Read types: only the 7 cloud-aligned vital types + sleep
    private let readTypes: Set<HKObjectType> = [
        HKObjectType.quantityType(forIdentifier: .heartRate)!,
        HKObjectType.quantityType(forIdentifier: .oxygenSaturation)!,
        HKObjectType.quantityType(forIdentifier: .stepCount)!,
        HKObjectType.quantityType(forIdentifier: .basalEnergyBurned)!,
        HKObjectType.quantityType(forIdentifier: .activeEnergyBurned)!,
        HKObjectType.quantityType(forIdentifier: .appleExerciseTime)!,
        HKObjectType.categoryType(forIdentifier: .appleStandHour)!,
        HKObjectType.categoryType(forIdentifier: .sleepAnalysis)!
    ]
    
    private let writeTypes: Set<HKSampleType> = [
        HKObjectType.quantityType(forIdentifier: .heartRate)!,
        HKObjectType.quantityType(forIdentifier: .oxygenSaturation)!,
        HKObjectType.quantityType(forIdentifier: .activeEnergyBurned)!
    ]
    
    // MARK: - Initialization
    
    init() {
        refreshAuthorizationStatus()
    }
    
    // MARK: - Authorization
    
    /// Check if HealthKit is available on this device
    var isHealthKitAvailable: Bool {
        return HKHealthStore.isHealthDataAvailable()
    }
    
    /// Request authorization to access HealthKit data
    func requestAuthorization() async throws {
        guard isHealthKitAvailable else {
            throw HealthKitError.notAvailable
        }
        
        try await healthStore.requestAuthorization(toShare: writeTypes, read: readTypes)
        
        await MainActor.run {
            refreshAuthorizationStatus()
        }
    }
    
    /// Refresh current authorization status (safe to call from outside)
    func refreshAuthorizationStatus() {
        guard isHealthKitAvailable else {
            authorizationStatus = .notDetermined
            isAuthorized = false
            return
        }
        
        // Check read authorization for heart rate as a representative sample
        if let heartRateType = HKObjectType.quantityType(forIdentifier: .heartRate) {
            authorizationStatus = healthStore.authorizationStatus(for: heartRateType)
            isAuthorized = authorizationStatus == .sharingAuthorized

            print("[HealthKitService] refreshAuthorizationStatus heartRateStatus=\(getAuthStatusDescription(authorizationStatus)) sleepStatus=\(getAuthStatusDescription(sleepAuthorizationStatus)) isAuthorized=\(isAuthorized)")
        }
    }
    
    // MARK: - Reading Health Data
    
    /// Read vital signs from HealthKit for a date range (7 cloud-aligned types only)
    func readVitalSigns(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard isAuthorized else {
            throw HealthKitError.notAuthorized
        }
        
        hkDebug("readVitalSigns window start=\(startDate.ISO8601Format()) end=\(endDate.ISO8601Format())")

        var vitalSigns: [VitalSignInfo] = []
        
        let heartRates = try await readHeartRate(from: startDate, to: endDate)
        let spO2 = try await readOxygenSaturation(from: startDate, to: endDate)
        let steps = try await readStepCount(from: startDate, to: endDate)
        let basalCalories = try await readBasalEnergyBurned(from: startDate, to: endDate)
        let activeEnergy = try await readActiveEnergyBurned(from: startDate, to: endDate)
        let exerciseMinutes = try await readExerciseMinutes(from: startDate, to: endDate)
        let standHours = try await readStandHours(from: startDate, to: endDate)

        hkDebug("readVitalSigns results heartRate=\(heartRates.count) spO2=\(spO2.count) stepsDaily=\(steps.count) basal=\(basalCalories.count) active=\(activeEnergy.count) exercise=\(exerciseMinutes.count) stand=\(standHours.count)")
        if let hr0 = heartRates.first { hkDebug("heartRate[0] value=\(hr0.value) ts=\(hr0.timestamp.ISO8601Format()) source=\(hr0.source)") }
        if let o20 = spO2.first { hkDebug("spO2[0] value=\(o20.value) ts=\(o20.timestamp.ISO8601Format()) source=\(o20.source)") }
        if let s0 = steps.first { hkDebug("stepsDaily[0] value=\(s0.value) ts=\(s0.timestamp.ISO8601Format()) source=\(s0.source)") }
        
        vitalSigns.append(contentsOf: heartRates)
        vitalSigns.append(contentsOf: spO2)
        vitalSigns.append(contentsOf: steps)
        vitalSigns.append(contentsOf: basalCalories)
        vitalSigns.append(contentsOf: activeEnergy)
        vitalSigns.append(contentsOf: exerciseMinutes)
        vitalSigns.append(contentsOf: standHours)
        
        return vitalSigns.sorted { $0.timestamp > $1.timestamp }
    }

    /// Read sleep sessions from HealthKit and merge contiguous/asleep-stage segments into session blocks.
    func readSleepSessions(from startDate: Date, to endDate: Date) async throws -> [SleepSessionInput] {
        guard isAuthorized else {
            print("[SleepDebug][HealthKitService] readSleepSessions skipped: service not authorized")
            throw HealthKitError.notAuthorized
        }

        guard let sleepType = HKObjectType.categoryType(forIdentifier: .sleepAnalysis) else {
            print("[SleepDebug][HealthKitService] readSleepSessions skipped: sleepAnalysis type unavailable")
            return []
        }

        // healthStore.authorizationStatus(for:) only returns WRITE (sharing) status.
        // If we try to check it for read-only types like Sleep, it will incorrectly return sharingDenied (1)
        // or notDetermined (0), and incorrectly block the query.
        // We must simply execute the query; if read is denied, HealthKit will silently return [] samples.

        let predicate = HKQuery.predicateForSamples(withStart: startDate, end: endDate, options: .strictStartDate)
        let sortDescriptor = NSSortDescriptor(key: HKSampleSortIdentifierStartDate, ascending: true)

        let samples: [HKSample] = try await withCheckedThrowingContinuation { continuation in
            let query = HKSampleQuery(
                sampleType: sleepType,
                predicate: predicate,
                limit: HKObjectQueryNoLimit,
                sortDescriptors: [sortDescriptor]
            ) { _, results, error in
                if let error = error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume(returning: results ?? [])
                }
            }

            healthStore.execute(query)
        }

        let asleepIntervals: [(start: Date, end: Date)] = samples.compactMap { sample in
            guard let categorySample = sample as? HKCategorySample else { return nil }
            guard isAsleepSleepValue(categorySample.value) else { return nil }
            guard categorySample.endDate > categorySample.startDate else { return nil }
            return (start: categorySample.startDate, end: categorySample.endDate)
        }

        let mergedIntervals = mergeSleepIntervals(asleepIntervals, maxGap: 45 * 60)

        return mergedIntervals
            .map { interval in
                let durationMinutes = max(Int(interval.end.timeIntervalSince(interval.start) / 60), 1)
                return SleepSessionInput(
                    startTime: interval.start,
                    endTime: interval.end,
                    durationMinutes: durationMinutes,
                    qualityScore: 0
                )
            }
            .sorted { $0.startTime > $1.startTime }
    }

    
    // MARK: - Specific Health Data Readers
    
    private func readHeartRate(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let heartRateType = HKQuantityType.quantityType(forIdentifier: .heartRate) else {
            return []
        }
        
        let samples = try await querySamples(for: heartRateType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: HKUnit.count().unitDivided(by: .minute()))
            
            return VitalSignInfo(
                id: UUID(),
                type: .heartRate,
                value: value,
                unit: "bpm",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readOxygenSaturation(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let oxygenType = HKQuantityType.quantityType(forIdentifier: .oxygenSaturation) else {
            return []
        }
        
        let samples = try await querySamples(for: oxygenType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .percent()) * 100
            
            return VitalSignInfo(
                id: UUID(),
                type: .spO2,
                value: value,
                unit: "%",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readStepCount(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let stepType = HKQuantityType.quantityType(forIdentifier: .stepCount) else {
            return []
        }

        let calendar = Calendar.current
        let from = calendar.startOfDay(for: startDate)
        let to = endDate

        hkDebug("readStepCount window start=\(from.ISO8601Format()) end=\(to.ISO8601Format()) (requested start=\(startDate.ISO8601Format()))")

        // HKStatisticsCollectionQuery is the most efficient way to aggregate daily steps, but can
        // occasionally return an empty collection depending on boundary/anchor alignment.
        // We keep a raw-samples fallback so step sync doesn't silently stop working.
        let daily = try await readDailyStepsWithStatisticsCollection(stepType: stepType, from: from, to: to, calendar: calendar)
        if !daily.isEmpty {
            hkDebug("readStepCount statisticsCollection dailyCount=\(daily.count) latestDay=\(daily.first?.timestamp.ISO8601Format() ?? "nil") latestSteps=\(daily.first?.value ?? 0)")
            return daily
        }

        let fallback = try await readDailyStepsWithRawSamples(stepType: stepType, from: from, to: to, calendar: calendar)
        hkDebug("readStepCount rawSamplesFallback dailyCount=\(fallback.count) latestDay=\(fallback.first?.timestamp.ISO8601Format() ?? "nil") latestSteps=\(fallback.first?.value ?? 0)")
        return fallback
    }

    private func readDailyStepsWithStatisticsCollection(
        stepType: HKQuantityType,
        from startDate: Date,
        to endDate: Date,
        calendar: Calendar
    ) async throws -> [VitalSignInfo] {
        let predicate = HKQuery.predicateForSamples(withStart: startDate, end: endDate, options: [.strictStartDate, .strictEndDate])

        var interval = DateComponents()
        interval.day = 1

        // Anchor at local midnight for stable day-bucketing.
        let anchorDate = calendar.startOfDay(for: Date())

        let query = HKStatisticsCollectionQuery(
            quantityType: stepType,
            quantitySamplePredicate: predicate,
            options: .cumulativeSum,
            anchorDate: anchorDate,
            intervalComponents: interval
        )

        return try await withCheckedThrowingContinuation { continuation in
            query.initialResultsHandler = { _, results, error in
                if let error = error {
                    continuation.resume(throwing: error)
                    return
                }

                guard let results else {
                    continuation.resume(returning: [])
                    return
                }

                var dailySteps: [VitalSignInfo] = []
                results.enumerateStatistics(from: startDate, to: endDate) { statistics, _ in
                    guard let sum = statistics.sumQuantity() else { return }
                    let value = sum.doubleValue(for: .count())
                    if value <= 0 { return }

                    dailySteps.append(VitalSignInfo(
                        id: UUID(),
                        type: .steps,
                        value: value,
                        unit: "steps",
                        source: "HealthKit",
                        timestamp: statistics.startDate,
                        isSynced: false
                    ))
                }

                if dailySteps.isEmpty {
                    // Helpful when users report "sleep works but steps don't": this confirms that the
                    // query ran but no daily buckets had data.
                    // (We also have a raw-sample fallback.)
                    continuation.resume(returning: [])
                    return
                }

                continuation.resume(returning: dailySteps)
            }

            healthStore.execute(query)
        }
    }

    private func readDailyStepsWithRawSamples(
        stepType: HKQuantityType,
        from startDate: Date,
        to endDate: Date,
        calendar: Calendar
    ) async throws -> [VitalSignInfo] {
        let predicate = HKQuery.predicateForSamples(withStart: startDate, end: endDate, options: [.strictStartDate, .strictEndDate])
        let sortDescriptor = NSSortDescriptor(key: HKSampleSortIdentifierStartDate, ascending: true)

        let samples: [HKSample] = try await withCheckedThrowingContinuation { continuation in
            let query = HKSampleQuery(
                sampleType: stepType,
                predicate: predicate,
                limit: HKObjectQueryNoLimit,
                sortDescriptors: [sortDescriptor]
            ) { _, results, error in
                if let error = error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume(returning: results ?? [])
                }
            }
            healthStore.execute(query)
        }

        hkDebug("readDailyStepsWithRawSamples sampleCount=\(samples.count)")
        if let first = samples.first as? HKQuantitySample {
            hkDebug("rawStepSample[0] value=\(first.quantity.doubleValue(for: .count())) start=\(first.startDate.ISO8601Format()) end=\(first.endDate.ISO8601Format()) source=\(first.sourceRevision.source.name)")
        }

        var totalsByDayStart: [Date: Double] = [:]
        for sample in samples {
            guard let qs = sample as? HKQuantitySample else { continue }
            let dayStart = calendar.startOfDay(for: qs.startDate)
            totalsByDayStart[dayStart, default: 0] += qs.quantity.doubleValue(for: .count())
        }

        return totalsByDayStart
            .filter { $0.value > 0 }
            .map { dayStart, total in
                VitalSignInfo(
                    id: UUID(),
                    type: .steps,
                    value: total,
                    unit: "steps",
                    source: "HealthKit",
                    timestamp: dayStart,
                    isSynced: false
                )
            }
            .sorted { $0.timestamp > $1.timestamp }
    }
    
    private func readBasalEnergyBurned(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let basalType = HKQuantityType.quantityType(forIdentifier: .basalEnergyBurned) else {
            return []
        }
        
        let samples = try await querySamples(for: basalType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .kilocalorie())
            
            return VitalSignInfo(
                id: UUID(),
                type: .calories,
                value: value,
                unit: "kcal",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readActiveEnergyBurned(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let energyType = HKQuantityType.quantityType(forIdentifier: .activeEnergyBurned) else {
            return []
        }
        
        let samples = try await querySamples(for: energyType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .kilocalorie())
            
            return VitalSignInfo(
                id: UUID(),
                type: .activeEnergy,
                value: value,
                unit: "kcal",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readExerciseMinutes(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let exerciseType = HKQuantityType.quantityType(forIdentifier: .appleExerciseTime) else {
            return []
        }
        
        let samples = try await querySamples(for: exerciseType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .minute())
            
            return VitalSignInfo(
                id: UUID(),
                type: .exerciseMinutes,
                value: value,
                unit: "min",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readStandHours(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let standType = HKObjectType.categoryType(forIdentifier: .appleStandHour) else {
            return []
        }
        
        let predicate = HKQuery.predicateForSamples(withStart: startDate, end: endDate, options: .strictStartDate)
        let sortDescriptor = NSSortDescriptor(key: HKSampleSortIdentifierStartDate, ascending: false)
        
        let samples: [HKSample] = try await withCheckedThrowingContinuation { continuation in
            let query = HKSampleQuery(
                sampleType: standType,
                predicate: predicate,
                limit: HKObjectQueryNoLimit,
                sortDescriptors: [sortDescriptor]
            ) { _, results, error in
                if let error = error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume(returning: results ?? [])
                }
            }
            healthStore.execute(query)
        }
        
        // Count stood hours for the period
        let stoodCount = samples
            .compactMap { $0 as? HKCategorySample }
            .filter { $0.value == HKCategoryValueAppleStandHour.stood.rawValue }
            .count
        
        guard stoodCount > 0 else { return [] }
        
        return [VitalSignInfo(
            id: UUID(),
            type: .standHours,
            value: Double(stoodCount),
            unit: "hrs",
            source: "HealthKit",
            timestamp: endDate,
            isSynced: false
        )]
    }
    
    // MARK: - Writing Health Data
    
    /// Write a vital sign to HealthKit
    func writeVitalSign(_ vitalSign: VitalSignInput) async throws {
        guard isAuthorized else {
            throw HealthKitError.notAuthorized
        }
        
        let sample = try createHealthKitSample(from: vitalSign)
        try await healthStore.save(sample)
    }
    
    /// Write multiple vital signs to HealthKit
    func writeVitalSigns(_ vitalSigns: [VitalSignInput]) async throws {
        guard isAuthorized else {
            throw HealthKitError.notAuthorized
        }
        
        let samples = try vitalSigns.map { try createHealthKitSample(from: $0) }
        try await healthStore.save(samples)
    }
    
    // MARK: - Private Helpers
    
    private func querySamples(for quantityType: HKQuantityType, from startDate: Date, to endDate: Date) async throws -> [HKSample] {
        let predicate = HKQuery.predicateForSamples(withStart: startDate, end: endDate, options: .strictStartDate)
        let sortDescriptor = NSSortDescriptor(key: HKSampleSortIdentifierStartDate, ascending: false)
        
        return try await withCheckedThrowingContinuation { continuation in
            let query = HKSampleQuery(
                sampleType: quantityType,
                predicate: predicate,
                limit: HKObjectQueryNoLimit,
                sortDescriptors: [sortDescriptor]
            ) { _, samples, error in
                if let error = error {
                    continuation.resume(throwing: error)
                } else {
                    if let samples = samples, !samples.isEmpty {
                        self.hkDebug("querySamples type=\(quantityType.identifier) count=\(samples.count) start=\(startDate.ISO8601Format()) end=\(endDate.ISO8601Format())")
                        if let first = samples.first as? HKQuantitySample {
                            self.hkDebug("querySamples first type=\(quantityType.identifier) start=\(first.startDate.ISO8601Format()) end=\(first.endDate.ISO8601Format()) source=\(first.sourceRevision.source.name)")
                        }
                    } else {
                        self.hkDebug("querySamples type=\(quantityType.identifier) count=0 start=\(startDate.ISO8601Format()) end=\(endDate.ISO8601Format())")
                    }
                    continuation.resume(returning: samples ?? [])
                }
            }
            
            healthStore.execute(query)
        }
    }

    private func isAsleepSleepValue(_ value: Int) -> Bool {
        // Phones without an Apple Watch mostly write ONLY .inBed.
        // We must include .inBed otherwise phone-only users will sync 0 sessions.
        // mergeSleepIntervals will naturally merge overlapping inBed + asleep segments.

        if #available(iOS 16.0, *), value == HKCategoryValueSleepAnalysis.awake.rawValue {
            return false // Explicitly awake
        }

        // Include .inBed, .asleepUnspecified, .asleepCore, .asleepDeep, .asleepREM
        return true
    }

    private func mergeSleepIntervals(_ intervals: [(start: Date, end: Date)], maxGap: TimeInterval) -> [(start: Date, end: Date)] {
        guard !intervals.isEmpty else { return [] }

        let sorted = intervals.sorted { $0.start < $1.start }
        var merged: [(start: Date, end: Date)] = [sorted[0]]

        for interval in sorted.dropFirst() {
            let lastIndex = merged.count - 1
            let last = merged[lastIndex]

            if interval.start.timeIntervalSince(last.end) <= maxGap {
                merged[lastIndex] = (start: last.start, end: max(last.end, interval.end))
            } else {
                merged.append(interval)
            }
        }

        return merged
    }
    
    private func createHealthKitSample(from vitalSign: VitalSignInput) throws -> HKQuantitySample {
        let (quantityType, unit) = try getHealthKitTypeAndUnit(for: vitalSign.type)
        let quantity = HKQuantity(unit: unit, doubleValue: vitalSign.value)
        let timestamp = vitalSign.timestamp ?? Date()
        
        return HKQuantitySample(
            type: quantityType,
            quantity: quantity,
            start: timestamp,
            end: timestamp
        )
    }
    
    private func getHealthKitTypeAndUnit(for vitalSignType: VitalSignType) throws -> (HKQuantityType, HKUnit) {
        switch vitalSignType {
        case .heartRate:
            guard let type = HKQuantityType.quantityType(forIdentifier: .heartRate) else {
                throw HealthKitError.unsupportedType
            }
            return (type, HKUnit.count().unitDivided(by: .minute()))
            
        case .spO2:
            guard let type = HKQuantityType.quantityType(forIdentifier: .oxygenSaturation) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .percent())
            
        case .steps:
            guard let type = HKQuantityType.quantityType(forIdentifier: .stepCount) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .count())
            
        case .calories:
            guard let type = HKQuantityType.quantityType(forIdentifier: .basalEnergyBurned) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .kilocalorie())
            
        case .activeEnergy:
            guard let type = HKQuantityType.quantityType(forIdentifier: .activeEnergyBurned) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .kilocalorie())
            
        case .exerciseMinutes:
            guard let type = HKQuantityType.quantityType(forIdentifier: .appleExerciseTime) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .minute())
            
        case .standHours:
            // Stand hours is a category type, not quantity - cannot create samples
            throw HealthKitError.unsupportedType
        }
    }
}

// MARK: - HealthKit Errors

enum HealthKitError: LocalizedError {
    case notAvailable
    case notAuthorized
    case unsupportedType
    case queryFailed(Error)
    
    var errorDescription: String? {
        switch self {
        case .notAvailable:
            return "HealthKit is not available on this device"
        case .notAuthorized:
            return "HealthKit access not authorized"
        case .unsupportedType:
            return "Unsupported HealthKit data type"
        case .queryFailed(let error):
            return "HealthKit query failed: \(error.localizedDescription)"
        }
    }
}