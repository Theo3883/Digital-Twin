import HealthKit
import Foundation

/// Service for integrating with HealthKit to read and write health data
@MainActor
class HealthKitService: ObservableObject {
    
    // MARK: - Properties
    
    private let healthStore = HKHealthStore()
    @Published var isAuthorized = false
    @Published var authorizationStatus: HKAuthorizationStatus = .notDetermined
    
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
        }
    }
    
    // MARK: - Reading Health Data
    
    /// Read vital signs from HealthKit for a date range (7 cloud-aligned types only)
    func readVitalSigns(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard isAuthorized else {
            throw HealthKitError.notAuthorized
        }
        
        var vitalSigns: [VitalSignInfo] = []
        
        let heartRates = try await readHeartRate(from: startDate, to: endDate)
        let spO2 = try await readOxygenSaturation(from: startDate, to: endDate)
        let steps = try await readStepCount(from: startDate, to: endDate)
        let basalCalories = try await readBasalEnergyBurned(from: startDate, to: endDate)
        let activeEnergy = try await readActiveEnergyBurned(from: startDate, to: endDate)
        let exerciseMinutes = try await readExerciseMinutes(from: startDate, to: endDate)
        let standHours = try await readStandHours(from: startDate, to: endDate)
        
        vitalSigns.append(contentsOf: heartRates)
        vitalSigns.append(contentsOf: spO2)
        vitalSigns.append(contentsOf: steps)
        vitalSigns.append(contentsOf: basalCalories)
        vitalSigns.append(contentsOf: activeEnergy)
        vitalSigns.append(contentsOf: exerciseMinutes)
        vitalSigns.append(contentsOf: standHours)
        
        return vitalSigns.sorted { $0.timestamp > $1.timestamp }
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
        
        let samples = try await querySamples(for: stepType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .count())
            
            return VitalSignInfo(
                id: UUID(),
                type: .steps,
                value: value,
                unit: "steps",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
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
                    continuation.resume(returning: samples ?? [])
                }
            }
            
            healthStore.execute(query)
        }
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