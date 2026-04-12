import Foundation

@MainActor
final class EngineDashboardRepository: DashboardRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func fetchSnapshot(from: Date?, to: Date?) async -> DashboardSnapshot {
        // Vitals always require a fresh DB read (time-range query)
        let vitals = await engine.getVitalSigns(from: from, to: to)

        // Environment: use in-memory cache if available; otherwise load from SQLite (no network)
        if engine.latestEnvironmentReading == nil {
            await engine.loadLatestEnvironmentReading()
        }

        // Sleep: use in-memory cache if available; otherwise load from SQLite (no network)
        if engine.sleepSessions.isEmpty {
            await engine.loadSleepSessions(from: from, to: to)
        }

        // Medications: read directly from cache — never call loadMedications() here.
        // loadMedications() triggers checkInteractions() → RxNav + OpenFDA HTTP requests.
        // The cache is pre-warmed by SyncGateView and invalidated automatically by performSync().

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
