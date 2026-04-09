import SwiftUI

struct EnvironmentView: View {
    @StateObject private var locationManager = LocationManager()
    @StateObject private var viewModel: EnvironmentViewModel
    @State private var isRefreshing = false
    @State private var showLocationSheet = false
    @State private var cityText = ""

    init(viewModel: EnvironmentViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                if let reading = viewModel.reading {
                    // AQI Hero Card
                    AQIHeroCard(reading: reading, onEditLocation: { showLocationSheet = true }, onRefresh: refresh)

                    // Metric Grid 2×3
                    MetricGrid(reading: reading)

                    // Correlation placeholder
                    CorrelationCard(reading: reading, latestHR: viewModel.latestHeartRate)

                    // AI Recommendation
                    AIRecommendationCard(reading: reading)

                    // Location bar
                    LocationInfoBar(reading: reading)
                } else {
                    EmptyEnvironmentView(onRefresh: refresh)
                }

                Spacer(minLength: 100)
            }
            .padding(16)
        }
        .pageEnterAnimation()
        .task {
            await viewModel.loadInitial()
            if viewModel.reading == nil {
                await fetchWithLocation()
            }
        }
        .refreshable { await fetchWithLocation() }
        .sheet(isPresented: $showLocationSheet) {
            LocationEditSheet(
                locationManager: locationManager,
                cityText: $cityText,
                onUseMyLocation: {
                    showLocationSheet = false
                    refresh()
                },
                onApplyCity: { city in
                    showLocationSheet = false
                    // TODO: geocode city and fetch
                }
            )
        }
    }

    private func refresh() {
        Task { await fetchWithLocation() }
    }

    private func fetchWithLocation() async {
        isRefreshing = true
        defer { isRefreshing = false }

        if let location = locationManager.lastLocation {
            await viewModel.fetch(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        } else {
            locationManager.requestLocation()
            try? await Task.sleep(for: .seconds(2))
            if let location = locationManager.lastLocation {
                await viewModel.fetch(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
            }
        }
    }
}
