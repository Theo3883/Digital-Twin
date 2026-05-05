import Foundation

struct GetEnvironmentAdviceUseCase: Sendable {
    private let repository: EnvironmentAnalyticsRepository

    init(repository: EnvironmentAnalyticsRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> CoachingAdviceInfo? {
        await repository.loadAdvice()
    }
}
