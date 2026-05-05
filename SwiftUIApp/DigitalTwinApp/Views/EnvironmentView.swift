import SwiftUI

struct EnvironmentView: View {
    @EnvironmentObject private var engineWrapper: MobileEngineWrapper
    @StateObject private var locationManager = LocationManager()
    @StateObject private var viewModel: EnvironmentViewModel
    @State private var isRefreshing = false
    @State private var showLocationSheet = false
    @State private var cityText = ""
    private let geocodingService = GeocodingService()

    init(viewModel: EnvironmentViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        ScrollView(showsIndicators: false) {
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
            .padding(.horizontal, 10)
            .padding(.top, 12)
            .padding(.bottom, 16)
        }
        .pageEnterAnimation()
        .task {
            await viewModel.loadInitial(preloaded: engineWrapper.latestEnvironmentReading)

            await fetchWithPreferredLocation()
        }
        .refreshable { await fetchWithPreferredLocation() }
        .sheet(isPresented: $showLocationSheet) {
            LocationEditSheet(
                locationManager: locationManager,
                cityText: $cityText,
                onUseMyLocation: {
                    showLocationSheet = false
                    LocationManager.setUseCurrentLocation()
                    refresh()
                },
                onApplyCity: { city in
                    showLocationSheet = false
                    Task {
                        if let result = await geocodingService.geocode(city: city) {
                            LocationManager.saveManualLocation(
                                latitude: result.latitude,
                                longitude: result.longitude,
                                displayName: result.displayName
                            )

                            await viewModel.fetch(latitude: result.latitude, longitude: result.longitude)
                        }
                    }
                }
            )
        }
    }

    private func refresh() {
        Task { await fetchWithPreferredLocation() }
    }

    private func fetchWithPreferredLocation() async {
        isRefreshing = true
        defer { isRefreshing = false }

        if let manual = LocationManager.manualLocationCoordinatesIfSelected() {
            await viewModel.fetch(latitude: manual.latitude, longitude: manual.longitude)
            return
        }

        if let location = locationManager.lastLocation {
            await viewModel.fetch(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
            return
        }

        locationManager.requestLocation()
        try? await Task.sleep(for: .seconds(2))

        if let location = locationManager.lastLocation {
            await viewModel.fetch(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
            return
        }

        if let cachedCurrent = LocationManager.cachedCurrentLocationCoordinates() {
            await viewModel.fetch(latitude: cachedCurrent.latitude, longitude: cachedCurrent.longitude)
            return
        }

        if let manualFallback = LocationManager.manualLocationCoordinates() {
            await viewModel.fetch(latitude: manualFallback.latitude, longitude: manualFallback.longitude)
        }
    }
}
