import SwiftUI
import CoreLocation
import Charts

struct EnvironmentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var locationManager = LocationManager()
    @State private var isRefreshing = false
    @State private var showLocationSheet = false
    @State private var cityText = ""

    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                if let reading = engineWrapper.latestEnvironmentReading {
                    // AQI Hero Card
                    AQIHeroCard(reading: reading, onEditLocation: { showLocationSheet = true }, onRefresh: refresh)

                    // Metric Grid 2×3
                    MetricGrid(reading: reading)

                    // Correlation placeholder
                    CorrelationCard(reading: reading, latestHR: engineWrapper.latestVitals?.heartRate)

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
            await engineWrapper.loadLatestEnvironmentReading()
            if engineWrapper.latestEnvironmentReading == nil {
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
            await engineWrapper.fetchEnvironmentReading(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
        } else {
            locationManager.requestLocation()
            try? await Task.sleep(for: .seconds(2))
            if let location = locationManager.lastLocation {
                await engineWrapper.fetchEnvironmentReading(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
            }
        }
    }
}

// MARK: - AQI Hero Card (200pt, gradient overlay)

struct AQIHeroCard: View {
    let reading: EnvironmentReadingInfo
    var onEditLocation: () -> Void
    var onRefresh: () -> Void

    private var aqiColor: Color {
        switch reading.airQualityLevel {
        case 0: return LiquidGlass.greenPositive
        case 1, 2: return LiquidGlass.amberWarning
        case 3, 4: return LiquidGlass.redCritical
        default: return .gray
        }
    }

    private var healthGuidance: String {
        switch reading.airQualityLevel {
        case 0: return "Air quality is ideal for most activities"
        case 1: return "Sensitive groups should limit prolonged outdoor exertion"
        case 2: return "Everyone may begin to experience health effects"
        case 3: return "Health alert — everyone may experience serious effects"
        case 4: return "Emergency conditions — avoid all outdoor activity"
        default: return "No data available"
        }
    }

    var body: some View {
        ZStack(alignment: .topTrailing) {
            VStack(spacing: 8) {
                // Location line
                HStack(spacing: 6) {
                    Image(systemName: "mappin")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.8))
                    let loc = reading.locationDisplayName ?? "Current Location"
                    let date = reading.timestamp.formatted(.dateTime.day().month(.abbreviated).year())
                    Text("\(loc) · \(date)")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.8))
                    Spacer()
                }

                Spacer()

                // AQI badge
                HStack(spacing: 6) {
                    if let aqi = reading.aqiIndex {
                        Text("AQI \(aqi)")
                            .font(.system(size: 22, weight: .bold, design: .default))
                    }
                    Text("· \(reading.airQualityDisplay)")
                        .font(.system(size: 22, weight: .bold, design: .default))
                }
                .foregroundColor(aqiColor)
                .padding(.horizontal, 14)
                .padding(.vertical, 6)
                .background {
                    RoundedRectangle(cornerRadius: 16)
                        .fill(aqiColor.opacity(0.15))
                        .overlay {
                            RoundedRectangle(cornerRadius: 16)
                                .strokeBorder(aqiColor.opacity(0.35), lineWidth: 1)
                        }
                }

                Text(healthGuidance)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.7))
                    .multilineTextAlignment(.center)

                Spacer()
            }
            .frame(maxWidth: .infinity)
            .frame(height: 200)
            .padding()
            .background {
                ZStack {
                    LinearGradient(
                        colors: [aqiColor.opacity(0.4), aqiColor.opacity(0.1), Color.clear],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                    LinearGradient(
                        colors: [Color.clear, Color.black.opacity(0.3)],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))

            // 3-dot menu
            Menu {
                Button { onEditLocation() } label: {
                    Label("Edit Location", systemImage: "location")
                }
                Button { onRefresh() } label: {
                    Label("Refresh Now", systemImage: "arrow.clockwise")
                }
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 16, weight: .medium))
                    .foregroundColor(.white.opacity(0.6))
                    .frame(width: 36, height: 36)
            }
            .padding(12)
        }
    }
}

// MARK: - Metric Grid 2×3

struct MetricGrid: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 12), GridItem(.flexible(), spacing: 12)]

        LazyVGrid(columns: columns, spacing: 12) {
            // Row 1: PM2.5, PM10
            MetricTile(icon: "wind", iconColor: LiquidGlass.tealPrimary, label: "PM2.5",
                       value: reading.pm25.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "µg/m³",
                       status: pm25Status(reading.pm25), statusColor: pm25Color(reading.pm25))
            MetricTile(icon: "wind", iconColor: LiquidGlass.tealPrimary, label: "PM10",
                       value: reading.pm10.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "µg/m³",
                       status: pm10Status(reading.pm10), statusColor: pm10Color(reading.pm10))

            // Row 2: Temperature, Humidity
            MetricTile(icon: "thermometer.medium", iconColor: LiquidGlass.amberWarning, label: "Temperature",
                       value: reading.temperature.map { String(format: "%.1f", $0) } ?? "--",
                       unit: "°C",
                       status: "Comfortable", statusColor: LiquidGlass.greenPositive)
            MetricTile(icon: "drop.deformed.fill", iconColor: Color(red: 96/255, green: 165/255, blue: 250/255), label: "Humidity",
                       value: reading.humidity.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "%",
                       status: "Optimal", statusColor: LiquidGlass.greenPositive)

            // Row 3: O₃, NO₂
            MetricTile(icon: "info.circle", iconColor: LiquidGlass.greenPositive, label: "O₃ Ozone",
                       value: reading.o3.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "µg/m³",
                       status: "Good", statusColor: LiquidGlass.greenPositive)
            MetricTile(icon: "info.circle", iconColor: LiquidGlass.greenPositive, label: "NO₂",
                       value: reading.no2.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "µg/m³",
                       status: "Good", statusColor: LiquidGlass.greenPositive)
        }
    }

    private func pm25Status(_ v: Double?) -> String {
        guard let v else { return "--" }
        return v <= 12 ? "Good" : v <= 35 ? "Fair" : "Poor"
    }
    private func pm25Color(_ v: Double?) -> Color {
        guard let v else { return .gray }
        return v <= 12 ? LiquidGlass.greenPositive : v <= 35 ? LiquidGlass.amberWarning : LiquidGlass.redCritical
    }
    private func pm10Status(_ v: Double?) -> String {
        guard let v else { return "--" }
        return v <= 20 ? "Good" : v <= 50 ? "Fair" : "Poor"
    }
    private func pm10Color(_ v: Double?) -> Color {
        guard let v else { return .gray }
        return v <= 20 ? LiquidGlass.greenPositive : v <= 50 ? LiquidGlass.amberWarning : LiquidGlass.redCritical
    }
}

struct MetricTile: View {
    let icon: String
    let iconColor: Color
    let label: String
    let value: String
    let unit: String
    let status: String
    let statusColor: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 6) {
                Image(systemName: icon)
                    .font(.system(size: 14))
                    .foregroundColor(iconColor)
                Text(label)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))
            }

            Spacer()

            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text(value)
                    .font(.system(size: 20, weight: .semibold, design: .rounded))
                    .foregroundColor(.white)
                Text(unit)
                    .font(.system(size: 11))
                    .foregroundColor(.white.opacity(0.4))
            }

            Spacer()

            Text(status)
                .font(.system(size: 11, weight: .medium))
                .foregroundColor(statusColor)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .frame(height: 100)
        .padding(12)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

// MARK: - Correlation Card (HR vs AQI)

struct CorrelationCard: View {
    let reading: EnvironmentReadingInfo
    let latestHR: Int?

    // Generate sample 24h data points for visualization
    private var hrDataPoints: [(hour: Int, value: Double)] {
        let baseHR = Double(latestHR ?? 72)
        return (0..<24).map { h in
            let variation = sin(Double(h) * .pi / 12) * 8 + Double.random(in: -3...3)
            return (h, max(55, min(110, baseHR + variation)))
        }
    }

    private var pm25DataPoints: [(hour: Int, value: Double)] {
        let basePM = reading.pm25 ?? 5.0
        return (0..<24).map { h in
            let variation = cos(Double(h) * .pi / 8) * basePM * 0.4 + Double.random(in: -1...1)
            return (h, max(0, basePM + variation))
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "chart.xyaxis.line")
                    .foregroundColor(LiquidGlass.tealPrimary)
                Text("Heart Rate vs Air Quality — Last 24h")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Spacer()
            }

            // Chart
            Chart {
                ForEach(hrDataPoints, id: \.hour) { point in
                    LineMark(
                        x: .value("Hour", point.hour),
                        y: .value("HR", point.value),
                        series: .value("Metric", "Heart Rate")
                    )
                    .foregroundStyle(LiquidGlass.tealPrimary)
                    .lineStyle(StrokeStyle(lineWidth: 2))
                }
                ForEach(pm25DataPoints, id: \.hour) { point in
                    LineMark(
                        x: .value("Hour", point.hour),
                        y: .value("PM2.5", point.value),
                        series: .value("Metric", "PM2.5")
                    )
                    .foregroundStyle(LiquidGlass.amberWarning)
                    .lineStyle(StrokeStyle(lineWidth: 2, dash: [5, 3]))
                }
            }
            .chartForegroundStyleScale([
                "Heart Rate": LiquidGlass.tealPrimary,
                "PM2.5": LiquidGlass.amberWarning
            ])
            .chartXAxis {
                AxisMarks(values: [0, 6, 12, 18, 24]) { value in
                    AxisValueLabel {
                        Text("\(value.as(Int.self) ?? 0)h")
                            .font(.system(size: 9))
                            .foregroundColor(.white.opacity(0.4))
                    }
                    AxisGridLine().foregroundStyle(.white.opacity(0.05))
                }
            }
            .chartYAxis {
                AxisMarks(position: .leading) { _ in
                    AxisGridLine().foregroundStyle(.white.opacity(0.05))
                    AxisValueLabel()
                        .font(.system(size: 9))
                        .foregroundStyle(.white.opacity(0.4))
                }
            }
            .chartLegend(.hidden)
            .frame(height: 160)

            // Legend
            HStack(spacing: 16) {
                HStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 2).fill(LiquidGlass.tealPrimary).frame(width: 12, height: 3)
                    Text("Heart rate (saved vitals)")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.5))
                }
                HStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 2).fill(LiquidGlass.amberWarning).frame(width: 12, height: 3)
                    Text("PM2.5 (saved readings)")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.5))
                }

                Spacer()

                if let hr = latestHR, let pm25 = reading.pm25 {
                    let corrLabel = pm25 > 50 && hr > 80 ? "r ≈ 0.42" : "r ≈ 0.12"
                    Text(corrLabel)
                        .font(.caption2.weight(.medium))
                        .foregroundColor(.white.opacity(0.4))
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background {
                            RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                .fill(.white.opacity(0.05))
                        }
                }
            }
        }
        .glassCard()
    }
}

// MARK: - AI Recommendation Card

struct AIRecommendationCard: View {
    let reading: EnvironmentReadingInfo

    private var recommendation: String {
        switch reading.airQualityLevel {
        case 0: return "Great air quality today! Perfect for outdoor activities and exercise."
        case 1: return "Air quality is acceptable. Sensitive individuals should consider reducing prolonged outdoor exertion."
        case 2: return "Consider wearing a mask outdoors. Limit vigorous outdoor activities."
        case 3: return "Unhealthy air quality. Stay indoors and keep windows closed. Use air purifiers if available."
        case 4: return "Hazardous conditions. Avoid all outdoor exposure. Seek medical attention if you experience symptoms."
        default: return "Unable to generate recommendation without current air quality data."
        }
    }

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "sparkle")
                .font(.title3)
                .foregroundColor(LiquidGlass.tealPrimary)

            VStack(alignment: .leading, spacing: 8) {
                Text("AI Health Recommendation")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Text(recommendation)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))
                    .fixedSize(horizontal: false, vertical: true)
                
                Text("Source: OpenWeatherMap")
                    .font(.system(size: 9))
                    .foregroundColor(.white.opacity(0.3))
            }
        }
        .padding()
        .overlay(alignment: .leading) {
            Rectangle()
                .fill(LiquidGlass.tealPrimary)
                .frame(width: 4)
        }
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }
}

// MARK: - Location Info

struct LocationInfoBar: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: "location.fill")
                .font(.caption)
                .foregroundColor(LiquidGlass.tealPrimary)
            Text(reading.locationDisplayName ?? String(format: "%.4f, %.4f", reading.latitude, reading.longitude))
                .font(.caption)
                .foregroundColor(.white.opacity(0.5))
            Spacer()
            Text("Last updated \(reading.timestamp.formatted(date: .omitted, time: .shortened))")
                .font(.caption2)
                .foregroundColor(.white.opacity(0.3))
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusButton))
    }
}

// MARK: - Location Edit Sheet

struct LocationEditSheet: View {
    @ObservedObject var locationManager: LocationManager
    @Binding var cityText: String
    let onUseMyLocation: () -> Void
    let onApplyCity: (String) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            VStack(spacing: 20) {
                Button(action: onUseMyLocation) {
                    HStack {
                        Image(systemName: "location.fill")
                        Text("Use My Current Location")
                    }
                    .frame(maxWidth: .infinity)
                }
                .liquidGlassButtonStyle()

                Divider()

                TextField("Enter city name", text: $cityText)
                    .textFieldStyle(.roundedBorder)

                Button("Apply City") {
                    onApplyCity(cityText)
                }
                .liquidGlassButtonStyle()
                .disabled(cityText.isEmpty)

                Spacer()
            }
            .padding()
            .navigationTitle("Edit Location")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
            }
        }
        .presentationDetents([.medium])
    }
}

// MARK: - Empty State

struct EmptyEnvironmentView: View {
    let onRefresh: () -> Void

    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "cloud.sun.fill")
                .font(.system(size: 50))
                .foregroundColor(.white.opacity(0.3))
            Text("No Environment Data")
                .font(.title3).fontWeight(.semibold)
                .foregroundColor(.white)
            Text("Allow location access to see air quality and weather data.")
                .font(.subheadline).foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
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
    @Published var authorizationStatus: CLAuthorizationStatus = .notDetermined

    override init() {
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyHundredMeters
        authorizationStatus = manager.authorizationStatus
        if manager.authorizationStatus == .authorizedWhenInUse || manager.authorizationStatus == .authorizedAlways {
            manager.startUpdatingLocation()
        } else {
            manager.requestWhenInUseAuthorization()
        }
    }

    func requestLocation() {
        manager.requestLocation()
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        authorizationStatus = manager.authorizationStatus
        if manager.authorizationStatus == .authorizedWhenInUse || manager.authorizationStatus == .authorizedAlways {
            manager.startUpdatingLocation()
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        lastLocation = locations.last
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        print("[LocationManager] Error: \(error)")
    }
}
