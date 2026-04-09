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
}

