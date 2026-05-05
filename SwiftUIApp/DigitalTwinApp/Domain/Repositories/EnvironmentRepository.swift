import Foundation

protocol EnvironmentRepository: Sendable {
    func loadLatest() async -> EnvironmentReadingInfo?
    func fetch(latitude: Double, longitude: Double) async -> EnvironmentReadingInfo?
    func latestHeartRate() async -> Int?
}

