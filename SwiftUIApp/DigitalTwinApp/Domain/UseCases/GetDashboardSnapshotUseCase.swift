import Foundation

struct GetDashboardSnapshotUseCase: Sendable {
    private let repository: DashboardRepository

    init(repository: DashboardRepository) {
        self.repository = repository
    }

    func callAsFunction(from: Date?, to: Date?) async -> DashboardSnapshot {
        await repository.fetchSnapshot(from: from, to: to)
    }
}

