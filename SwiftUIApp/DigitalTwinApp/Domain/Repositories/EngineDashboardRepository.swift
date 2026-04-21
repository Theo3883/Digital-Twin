import Foundation

@MainActor
final class EngineDashboardRepository: DashboardRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func fetchSnapshot(from: Date?, to: Date?) async -> DashboardSnapshot {
        func formatDate(_ value: Date?) -> String {
            guard let value else { return "nil" }
            return ISO8601DateFormatter().string(from: value)
        }

        print("[SleepDebug][DashboardRepo] fetchSnapshot start. vitalsRangeFrom=\(formatDate(from)) vitalsRangeTo=\(formatDate(to))")

        // Vitals always require a fresh DB read (time-range query)
        let vitals = await engine.getVitalSigns(from: from, to: to)

        // Environment: use in-memory cache if available; otherwise load from SQLite (no network)
        if engine.latestEnvironmentReading == nil {
            await engine.loadLatestEnvironmentReading()
        }

        // Sleep for Home: load all sessions and select latest in presentation layer.
        // This avoids accidental empty cards when the latest session is outside a short range window.
        await engine.loadSleepSessionsFromLocalStoreOnly(from: nil, to: nil)

        let sleepSessions = engine.sleepSessions.sorted { $0.endTime > $1.endTime }

        if let latest = sleepSessions.first {
            print("[SleepDebug][DashboardRepo] loaded sleepSessions=\(sleepSessions.count) latestEnd=\(ISO8601DateFormatter().string(from: latest.endTime)) latestDurationMin=\(latest.durationMinutes) latestQuality=\(latest.qualityScore)")
        } else {
            print("[SleepDebug][DashboardRepo] loaded sleepSessions=0 (no sessions found in local store)")
        }

        // Medications: read directly from cache — never call loadMedications() here.
        // loadMedications() triggers checkInteractions() → RxNav + OpenFDA HTTP requests.
        // The cache is pre-warmed by SyncGateView and invalidated automatically by performSync().

        let snapshot = DashboardSnapshot(
            user: engine.currentUser,
            patientProfile: engine.patientProfile,
            recentVitals: vitals,
            coachingAdvice: engine.coachingAdvice,
            environmentReading: engine.latestEnvironmentReading,
            sleepSessions: sleepSessions,
            medications: engine.medications
        )

        print("[SleepDebug][DashboardRepo] snapshot built. sleepCount=\(snapshot.sleepSessions.count) vitalsCount=\(snapshot.recentVitals.count)")

        return snapshot
    }
}
