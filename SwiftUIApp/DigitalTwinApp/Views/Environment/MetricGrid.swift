import SwiftUI

struct MetricGrid: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 8), GridItem(.flexible(), spacing: 8)]

        LazyVGrid(columns: columns, spacing: 8) {
            MetricTile(label: "PM2.5", topSymbol: "wind", symbolColor: LiquidGlass.tealPrimary,
                       value: String(format: "%.0f", reading.pm25),
                       unit: "µg/m³",
                       status: pm25Status(reading.pm25), statusColor: pm25Color(reading.pm25))
            MetricTile(label: "PM10", topSymbol: "aqi.medium", symbolColor: LiquidGlass.tealPrimary,
                       value: String(format: "%.0f", reading.pm10),
                       unit: "µg/m³",
                       status: pm10Status(reading.pm10), statusColor: pm10Color(reading.pm10))

            MetricTile(label: "Temperature", topSymbol: "thermometer.medium", symbolColor: LiquidGlass.amberWarning,
                       value: String(format: "%.1f", reading.temperature),
                       unit: "°C",
                       status: "Comfortable", statusColor: LiquidGlass.greenPositive)
            MetricTile(label: "Humidity", topSymbol: "humidity.fill", symbolColor: Color(red: 96/255, green: 165/255, blue: 250/255),
                       value: String(format: "%.0f", reading.humidity),
                       unit: "%",
                       status: "Optimal", statusColor: LiquidGlass.greenPositive)

            MetricTile(label: "O₃ (Ozone)", topSymbol: "info.circle", symbolColor: LiquidGlass.greenPositive,
                       value: String(format: "%.0f", reading.o3),
                       unit: "µg/m³",
                       status: "Good", statusColor: LiquidGlass.greenPositive)
            MetricTile(label: "NO₂", topSymbol: "info.circle", symbolColor: LiquidGlass.greenPositive,
                       value: String(format: "%.0f", reading.no2),
                       unit: "µg/m³",
                       status: "Good", statusColor: LiquidGlass.greenPositive)
        }
    }

    private func pm25Status(_ v: Double) -> String {
        return v <= 12 ? "Good" : v <= 35 ? "Fair" : "Poor"
    }

    private func pm25Color(_ v: Double) -> Color {
        return v <= 12 ? LiquidGlass.greenPositive : v <= 35 ? LiquidGlass.amberWarning : LiquidGlass.redCritical
    }

    private func pm10Status(_ v: Double) -> String {
        return v <= 20 ? "Good" : v <= 50 ? "Fair" : "Poor"
    }

    private func pm10Color(_ v: Double) -> Color {
        return v <= 20 ? LiquidGlass.greenPositive : v <= 50 ? LiquidGlass.amberWarning : LiquidGlass.redCritical
    }
}

