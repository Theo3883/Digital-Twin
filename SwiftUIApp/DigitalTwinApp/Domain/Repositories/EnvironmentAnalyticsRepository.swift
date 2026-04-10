import Foundation

protocol EnvironmentAnalyticsRepository: Sendable {
    func loadAnalytics() async -> EnvironmentAnalyticsInfo?
    func loadAdvice() async -> CoachingAdviceInfo?
}
