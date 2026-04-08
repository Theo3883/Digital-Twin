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
    
    private let readTypes: Set<HKObjectType> = [
        HKObjectType.quantityType(forIdentifier: .heartRate)!,
        HKObjectType.quantityType(forIdentifier: .bloodPressureSystolic)!,
        HKObjectType.quantityType(forIdentifier: .bloodPressureDiastolic)!,
        HKObjectType.quantityType(forIdentifier: .bodyTemperature)!,
        HKObjectType.quantityType(forIdentifier: .oxygenSaturation)!,
        HKObjectType.quantityType(forIdentifier: .respiratoryRate)!,
        HKObjectType.quantityType(forIdentifier: .bloodGlucose)!,
        HKObjectType.quantityType(forIdentifier: .bodyMass)!,
        HKObjectType.quantityType(forIdentifier: .height)!,
        HKObjectType.quantityType(forIdentifier: .bodyMassIndex)!,
        HKObjectType.quantityType(forIdentifier: .stepCount)!,
        HKObjectType.quantityType(forIdentifier: .activeEnergyBurned)!,
        HKObjectType.categoryType(forIdentifier: .sleepAnalysis)!
    ]
    
    private let writeTypes: Set<HKSampleType> = [
        HKObjectType.quantityType(forIdentifier: .heartRate)!,
        HKObjectType.quantityType(forIdentifier: .bloodPressureSystolic)!,
        HKObjectType.quantityType(forIdentifier: .bloodPressureDiastolic)!,
        HKObjectType.quantityType(forIdentifier: .bodyTemperature)!,
        HKObjectType.quantityType(forIdentifier: .oxygenSaturation)!,
        HKObjectType.quantityType(forIdentifier: .respiratoryRate)!,
        HKObjectType.quantityType(forIdentifier: .bloodGlucose)!,
        HKObjectType.quantityType(forIdentifier: .bodyMass)!,
        HKObjectType.quantityType(forIdentifier: .height)!
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
    
    /// Read vital signs from HealthKit for a date range
    func readVitalSigns(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard isAuthorized else {
            throw HealthKitError.notAuthorized
        }
        
        var vitalSigns: [VitalSignInfo] = []
        
        // Read different types of vital signs
        let heartRates = try await readHeartRate(from: startDate, to: endDate)
        let bloodPressures = try await readBloodPressure(from: startDate, to: endDate)
        let temperatures = try await readBodyTemperature(from: startDate, to: endDate)
        let oxygenSaturations = try await readOxygenSaturation(from: startDate, to: endDate)
        let respiratoryRates = try await readRespiratoryRate(from: startDate, to: endDate)
        let bloodGlucose = try await readBloodGlucose(from: startDate, to: endDate)
        let weights = try await readBodyMass(from: startDate, to: endDate)
        let heights = try await readHeight(from: startDate, to: endDate)
        let steps = try await readStepCount(from: startDate, to: endDate)
        let calories = try await readActiveEnergyBurned(from: startDate, to: endDate)
        
        vitalSigns.append(contentsOf: heartRates)
        vitalSigns.append(contentsOf: bloodPressures)
        vitalSigns.append(contentsOf: temperatures)
        vitalSigns.append(contentsOf: oxygenSaturations)
        vitalSigns.append(contentsOf: respiratoryRates)
        vitalSigns.append(contentsOf: bloodGlucose)
        vitalSigns.append(contentsOf: weights)
        vitalSigns.append(contentsOf: heights)
        vitalSigns.append(contentsOf: steps)
        vitalSigns.append(contentsOf: calories)
        
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
    
    private func readBloodPressure(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let systolicType = HKQuantityType.quantityType(forIdentifier: .bloodPressureSystolic),
              let diastolicType = HKQuantityType.quantityType(forIdentifier: .bloodPressureDiastolic) else {
            return []
        }
        
        let systolicSamples = try await querySamples(for: systolicType, from: startDate, to: endDate)
        let diastolicSamples = try await querySamples(for: diastolicType, from: startDate, to: endDate)
        
        var bloodPressures: [VitalSignInfo] = []
        
        // Group systolic and diastolic readings by correlation
        let correlatedReadings = correlateBloodPressureReadings(
            systolic: systolicSamples.compactMap { $0 as? HKQuantitySample },
            diastolic: diastolicSamples.compactMap { $0 as? HKQuantitySample }
        )
        
        for (systolic, diastolic) in correlatedReadings {
            let systolicValue = systolic.quantity.doubleValue(for: .millimeterOfMercury())
            _ = diastolic.quantity.doubleValue(for: .millimeterOfMercury())
            
            bloodPressures.append(VitalSignInfo(
                id: UUID(),
                type: .bloodPressure,
                value: systolicValue, // Store systolic as primary value
                unit: "mmHg",
                source: "HealthKit",
                timestamp: systolic.startDate,
                isSynced: false
            ))
        }
        
        return bloodPressures
    }
    
    private func readBodyTemperature(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let temperatureType = HKQuantityType.quantityType(forIdentifier: .bodyTemperature) else {
            return []
        }
        
        let samples = try await querySamples(for: temperatureType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .degreeFahrenheit())
            
            return VitalSignInfo(
                id: UUID(),
                type: .temperature,
                value: value,
                unit: "°F",
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
                type: .oxygenSaturation,
                value: value,
                unit: "%",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readRespiratoryRate(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let respiratoryType = HKQuantityType.quantityType(forIdentifier: .respiratoryRate) else {
            return []
        }
        
        let samples = try await querySamples(for: respiratoryType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: HKUnit.count().unitDivided(by: .minute()))
            
            return VitalSignInfo(
                id: UUID(),
                type: .respiratoryRate,
                value: value,
                unit: "breaths/min",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readBloodGlucose(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let glucoseType = HKQuantityType.quantityType(forIdentifier: .bloodGlucose) else {
            return []
        }
        
        let samples = try await querySamples(for: glucoseType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: HKUnit.gramUnit(with: .milli).unitDivided(by: .literUnit(with: .deci)))
            
            return VitalSignInfo(
                id: UUID(),
                type: .bloodGlucose,
                value: value,
                unit: "mg/dL",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readBodyMass(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let massType = HKQuantityType.quantityType(forIdentifier: .bodyMass) else {
            return []
        }
        
        let samples = try await querySamples(for: massType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .pound())
            
            return VitalSignInfo(
                id: UUID(),
                type: .weight,
                value: value,
                unit: "lbs",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
    }
    
    private func readHeight(from startDate: Date, to endDate: Date) async throws -> [VitalSignInfo] {
        guard let heightType = HKQuantityType.quantityType(forIdentifier: .height) else {
            return []
        }
        
        let samples = try await querySamples(for: heightType, from: startDate, to: endDate)
        
        return samples.compactMap { sample in
            guard let quantitySample = sample as? HKQuantitySample else { return nil }
            
            let value = quantitySample.quantity.doubleValue(for: .inch())
            
            return VitalSignInfo(
                id: UUID(),
                type: .height,
                value: value,
                unit: "in",
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
                type: .stepCount,
                value: value,
                unit: "steps",
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
                type: .caloriesBurned,
                value: value,
                unit: "cal",
                source: "HealthKit",
                timestamp: quantitySample.startDate,
                isSynced: false
            )
        }
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
    
    private func correlateBloodPressureReadings(systolic: [HKQuantitySample], diastolic: [HKQuantitySample]) -> [(HKQuantitySample, HKQuantitySample)] {
        var correlatedReadings: [(HKQuantitySample, HKQuantitySample)] = []
        
        for systolicSample in systolic {
            // Find the closest diastolic reading within a reasonable time window (5 minutes)
            let timeWindow: TimeInterval = 5 * 60 // 5 minutes
            
            let closestDiastolic = diastolic.min { sample1, sample2 in
                let diff1 = abs(sample1.startDate.timeIntervalSince(systolicSample.startDate))
                let diff2 = abs(sample2.startDate.timeIntervalSince(systolicSample.startDate))
                return diff1 < diff2
            }
            
            if let diastolicSample = closestDiastolic,
               abs(diastolicSample.startDate.timeIntervalSince(systolicSample.startDate)) <= timeWindow {
                correlatedReadings.append((systolicSample, diastolicSample))
            }
        }
        
        return correlatedReadings
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
            
        case .bloodPressure:
            guard let type = HKQuantityType.quantityType(forIdentifier: .bloodPressureSystolic) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .millimeterOfMercury())
            
        case .temperature:
            guard let type = HKQuantityType.quantityType(forIdentifier: .bodyTemperature) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .degreeFahrenheit())
            
        case .oxygenSaturation:
            guard let type = HKQuantityType.quantityType(forIdentifier: .oxygenSaturation) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .percent())
            
        case .respiratoryRate:
            guard let type = HKQuantityType.quantityType(forIdentifier: .respiratoryRate) else {
                throw HealthKitError.unsupportedType
            }
            return (type, HKUnit.count().unitDivided(by: .minute()))
            
        case .bloodGlucose:
            guard let type = HKQuantityType.quantityType(forIdentifier: .bloodGlucose) else {
                throw HealthKitError.unsupportedType
            }
            return (type, HKUnit.gramUnit(with: .milli).unitDivided(by: .literUnit(with: .deci)))
            
        case .weight:
            guard let type = HKQuantityType.quantityType(forIdentifier: .bodyMass) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .pound())
            
        case .height:
            guard let type = HKQuantityType.quantityType(forIdentifier: .height) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .inch())
            
        case .stepCount:
            guard let type = HKQuantityType.quantityType(forIdentifier: .stepCount) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .count())
            
        case .caloriesBurned:
            guard let type = HKQuantityType.quantityType(forIdentifier: .activeEnergyBurned) else {
                throw HealthKitError.unsupportedType
            }
            return (type, .kilocalorie())
            
        case .bmi, .sleepDuration:
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