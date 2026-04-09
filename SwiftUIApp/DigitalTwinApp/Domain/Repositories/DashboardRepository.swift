import Foundation

protocol DashboardRepository: Sendable {
    func fetchSnapshot(from: Date?, to: Date?) async -> DashboardSnapshot
}

