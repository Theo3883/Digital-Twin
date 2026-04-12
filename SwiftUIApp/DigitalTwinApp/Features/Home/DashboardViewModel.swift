import Foundation

@MainActor
final class DashboardViewModel: ObservableObject {
    @Published private(set) var snapshot: DashboardSnapshot?
    @Published private(set) var isLoading = false
    @Published private(set) var hasLoaded = false

    private let getSnapshot: GetDashboardSnapshotUseCase

    init(getSnapshot: GetDashboardSnapshotUseCase) {
        self.getSnapshot = getSnapshot
    }

    /// Instantly populate snapshot from in-memory session store cache (no await, no network).
    /// Call this before load() to prevent the "no profile" flash on first render.
    func preload(engine: MobileEngineWrapper) {
        guard snapshot == nil else { return }
        snapshot = DashboardSnapshot(
            user: engine.currentUser,
            patientProfile: engine.patientProfile,
            recentVitals: [],
            coachingAdvice: engine.coachingAdvice,
            environmentReading: engine.latestEnvironmentReading,
            sleepSessions: engine.sleepSessions,
            medications: engine.medications
        )
    }

    /// Full async load — reads vitals from DB and rebuilds snapshot.
    func load() async {
        isLoading = true
        defer { isLoading = false }

        let fromDate = Calendar.current.date(byAdding: .day, value: -7, to: Date())
        snapshot = await getSnapshot(from: fromDate, to: Date())
        hasLoaded = true
    }

    /// Load only if not already loaded — avoids re-fetching on re-navigation.
    func loadIfNeeded() async {
        guard !hasLoaded else { return }
        await load()
    }

    func refreshCoachingAdvice(using engine: MobileEngineWrapper, forceRefresh: Bool = false) async {
        guard snapshot?.patientProfile != nil else { return }

        await engine.fetchCoachingAdvice(forceRefresh: forceRefresh)

        guard let current = snapshot else { return }
        snapshot = DashboardSnapshot(
            user: current.user,
            patientProfile: current.patientProfile,
            recentVitals: current.recentVitals,
            coachingAdvice: engine.coachingAdvice,
            environmentReading: current.environmentReading,
            sleepSessions: current.sleepSessions,
            medications: current.medications
        )
    }
}
