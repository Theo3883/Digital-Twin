import Foundation

@MainActor
final class EngineEnvironmentRepository: EnvironmentRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadLatest() async -> EnvironmentReadingInfo? {
        await engine.loadLatestEnvironmentReading()
        return engine.latestEnvironmentReading
    }

    func fetch(latitude: Double, longitude: Double) async -> EnvironmentReadingInfo? {
        await engine.fetchEnvironmentReading(latitude: latitude, longitude: longitude)
        return engine.latestEnvironmentReading
    }

    func latestHeartRate() async -> Int? {
        engine.latestVitals?.heartRate
    }
}

