import Foundation

@MainActor
final class EnvironmentViewModel: ObservableObject {
    @Published private(set) var reading: EnvironmentReadingInfo?
    @Published private(set) var latestHeartRate: Int?
    @Published private(set) var isRefreshing: Bool = false

    private let loadLatest: LoadLatestEnvironmentReadingUseCase
    private let fetchReading: FetchEnvironmentReadingUseCase
    private let repository: EnvironmentRepository

    init(loadLatest: LoadLatestEnvironmentReadingUseCase, fetchReading: FetchEnvironmentReadingUseCase, repository: EnvironmentRepository) {
        self.loadLatest = loadLatest
        self.fetchReading = fetchReading
        self.repository = repository
    }

    func loadInitial() async {
        reading = await loadLatest()
        latestHeartRate = await repository.latestHeartRate()
    }

    func fetch(latitude: Double, longitude: Double) async {
        isRefreshing = true
        defer { isRefreshing = false }

        reading = await fetchReading(latitude: latitude, longitude: longitude)
        latestHeartRate = await repository.latestHeartRate()
    }
}

