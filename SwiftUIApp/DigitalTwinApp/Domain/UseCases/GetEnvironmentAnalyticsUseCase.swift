import Foundation

struct GetEnvironmentAnalyticsUseCase: Sendable {
    private let repository: EnvironmentAnalyticsRepository

    init(repository: EnvironmentAnalyticsRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> EnvironmentAnalyticsInfo? {
        await repository.loadAnalytics()
    }
}
