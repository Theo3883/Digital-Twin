import Foundation

@MainActor
final class EngineEnvironmentAnalyticsRepository: EnvironmentAnalyticsRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadAnalytics() async -> EnvironmentAnalyticsInfo? {
        await engine.loadEnvironmentAnalytics()
        return engine.environmentAnalytics
    }

    func loadAdvice() async -> CoachingAdviceInfo? {
        await engine.loadEnvironmentAdvice()
        return engine.environmentAdvice
    }
}
