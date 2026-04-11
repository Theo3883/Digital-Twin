import Foundation

@MainActor
final class DashboardViewModel: ObservableObject {
    @Published private(set) var snapshot: DashboardSnapshot?
    @Published private(set) var isLoading = false

    private let getSnapshot: GetDashboardSnapshotUseCase

    init(getSnapshot: GetDashboardSnapshotUseCase) {
        self.getSnapshot = getSnapshot
    }

    func load() async {
        isLoading = true
        defer { isLoading = false }

        let fromDate = Calendar.current.date(byAdding: .day, value: -7, to: Date())
        snapshot = await getSnapshot(from: fromDate, to: Date())
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

