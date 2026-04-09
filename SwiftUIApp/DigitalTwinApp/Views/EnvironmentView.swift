import SwiftUI
import CoreLocation

struct EnvironmentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var locationManager = LocationManager()
    @State private var isRefreshing = false

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 20) {
                    if let reading = engineWrapper.latestEnvironmentReading {
                        // AQI Hero Card
                        AQIHeroCard(reading: reading)

                        // Weather Section
                        WeatherSection(reading: reading)

                        // Pollutant Details
                        PollutantGrid(reading: reading)

                        // Location Info
                        LocationInfoBar(reading: reading)
                    } else {
                        EmptyEnvironmentView(onRefresh: refresh)
                    }

                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("Environment")
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: refresh) {
                        Image(systemName: "arrow.clockwise")
                    }
                    .liquidGlassButtonStyle()
                    .disabled(isRefreshing)
                }
            }
            .task {
                await engineWrapper.loadLatestEnvironmentReading()
                if engineWrapper.latestEnvironmentReading == nil {
                    await fetchWithLocation()
                }
            }
            .refreshable { await fetchWithLocation() }
        }
    }

    private func refresh() {
        Task { await fetchWithLocation() }
    }

    private func fetchWithLocation() async {
        isRefreshing = true
        defer { isRefreshing = false }

        if let location = locationManager.lastLocation {
            await engineWrapper.fetchEnvironmentReading(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        } else {
            locationManager.requestLocation()
            // Wait briefly for location
            try? await Task.sleep(for: .seconds(2))
            if let location = locationManager.lastLocation {
                await engineWrapper.fetchEnvironmentReading(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
            }
        }
    }
}

// MARK: - AQI Hero Card

struct AQIHeroCard: View {
    let reading: EnvironmentReadingInfo

    private var tintColor: Color {
        switch reading.airQualityLevel {
        case 0: return LiquidGlass.greenPositive
        case 1: return .yellow
        case 2: return .orange
        case 3: return LiquidGlass.redCritical
        case 4: return .purple
        default: return .gray
        }
    }

    var body: some View {
        VStack(spacing: 12) {
            Text(reading.airQualityEmoji)
                .font(.system(size: 50))

            Text("Air Quality: \(reading.airQualityDisplay)")
                .font(.title2).fontWeight(.bold)

            if let aqi = reading.aqiIndex {
                Text("AQI \(aqi)")
                    .font(.largeTitle).fontWeight(.heavy)
            }

            Text(reading.timestamp.formatted(date: .abbreviated, time: .shortened))
                .font(.caption).foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
        .glassHeroCard(tint: tintColor)
    }
}

// MARK: - Weather Section

struct WeatherSection: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        HStack(spacing: 12) {
            if let temp = reading.temperature {
                WeatherTile(title: "Temperature", value: String(format: "%.1f°C", temp), icon: "thermometer", color: .orange)
            }
            if let humidity = reading.humidity {
                WeatherTile(title: "Humidity", value: String(format: "%.0f%%", humidity), icon: "humidity.fill", color: .cyan)
            }
        }
    }
}

struct WeatherTile: View {
    let title: String
    let value: String
    let icon: String
    let color: Color

    var body: some View {
        VStack(spacing: 8) {
            Image(systemName: icon)
                .font(.title2).foregroundColor(color)
            Text(value)
                .font(.title3).fontWeight(.semibold)
            Text(title)
                .font(.caption).foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
        .glassCard(tint: color.opacity(0.3))
    }
}

// MARK: - Pollutant Grid

struct PollutantGrid: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Pollutants")
                .glassSectionHeader()

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                if let pm25 = reading.pm25 {
                    PollutantTile(name: "PM2.5", value: pm25, unit: "µg/m³", threshold: 25)
                }
                if let pm10 = reading.pm10 {
                    PollutantTile(name: "PM10", value: pm10, unit: "µg/m³", threshold: 50)
                }
                if let o3 = reading.o3 {
                    PollutantTile(name: "O₃", value: o3, unit: "µg/m³", threshold: 100)
                }
                if let no2 = reading.no2 {
                    PollutantTile(name: "NO₂", value: no2, unit: "µg/m³", threshold: 40)
                }
            }
        }
    }
}

struct PollutantTile: View {
    let name: String
    let value: Double
    let unit: String
    let threshold: Double

    private var tint: Color {
        value > threshold * 2 ? LiquidGlass.redCritical :
        value > threshold ? LiquidGlass.amberWarning :
        LiquidGlass.greenPositive
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(name).font(.caption).foregroundColor(.secondary)
            Text(String(format: "%.1f", value))
                .font(.title3).fontWeight(.semibold)
            Text(unit).font(.caption2).foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassCard(tint: tint.opacity(0.3))
    }
}

// MARK: - Location Info

struct LocationInfoBar: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        HStack {
            Image(systemName: "location.fill")
                .foregroundColor(.blue)
            Text(reading.locationDisplayName ?? String(format: "%.4f, %.4f", reading.latitude, reading.longitude))
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
        .glassCard()
    }
}

// MARK: - Empty State

struct EmptyEnvironmentView: View {
    let onRefresh: () -> Void

    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "cloud.sun.fill")
                .font(.system(size: 50)).foregroundColor(.secondary)
            Text("No Environment Data")
                .font(.title3).fontWeight(.semibold)
            Text("Allow location access to see air quality and weather data.")
                .font(.subheadline).foregroundColor(.secondary).multilineTextAlignment(.center)
            Button("Refresh", action: onRefresh)
                .liquidGlassButtonStyle()
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}

// MARK: - Location Manager

class LocationManager: NSObject, ObservableObject, CLLocationManagerDelegate {
    private let manager = CLLocationManager()
    @Published var lastLocation: CLLocation?

    override init() {
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyHundredMeters
        manager.requestWhenInUseAuthorization()
        manager.startUpdatingLocation()
    }

    func requestLocation() {
        manager.requestLocation()
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        lastLocation = locations.last
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        print("[LocationManager] Error: \(error)")
    }
}
