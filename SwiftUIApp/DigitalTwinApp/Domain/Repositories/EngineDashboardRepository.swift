import Foundation

@MainActor
final class EngineDashboardRepository: DashboardRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func fetchSnapshot(from: Date?, to: Date?) async -> DashboardSnapshot {
        let vitals = await engine.getVitalSigns(from: from, to: to)
        await engine.loadLatestEnvironmentReading()
        await engine.loadSleepSessions(from: from, to: to)
        await engine.loadMedications()

        return DashboardSnapshot(
            user: engine.currentUser,
            patientProfile: engine.patientProfile,
            recentVitals: vitals,
            coachingAdvice: engine.coachingAdvice,
            environmentReading: engine.latestEnvironmentReading,
            sleepSessions: engine.sleepSessions,
            medications: engine.medications
        )
    }
}

