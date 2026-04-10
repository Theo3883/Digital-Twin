import SwiftUI

struct MetricGrid: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 12), GridItem(.flexible(), spacing: 12)]

        LazyVGrid(columns: columns, spacing: 12) {
            MetricTile(icon: "wind", iconColor: LiquidGlass.tealPrimary, label: "PM2.5",
                       value: String(format: "%.0f", reading.pm25),
                       unit: "µg/m³",
                       status: pm25Status(reading.pm25), statusColor: pm25Color(reading.pm25))
            MetricTile(icon: "wind", iconColor: LiquidGlass.tealPrimary, label: "PM10",
                       value: String(format: "%.0f", reading.pm10),
                       unit: "µg/m³",
                       status: pm10Status(reading.pm10), statusColor: pm10Color(reading.pm10))

            MetricTile(icon: "thermometer.medium", iconColor: LiquidGlass.amberWarning, label: "Temperature",
                       value: String(format: "%.1f", reading.temperature),
                       unit: "°C",
                       status: "Comfortable", statusColor: LiquidGlass.greenPositive)
            MetricTile(icon: "drop.deformed.fill", iconColor: Color(red: 96/255, green: 165/255, blue: 250/255), label: "Humidity",
                       value: String(format: "%.0f", reading.humidity),
                       unit: "%",
                       status: "Optimal", statusColor: LiquidGlass.greenPositive)

            MetricTile(icon: "info.circle", iconColor: LiquidGlass.greenPositive, label: "O₃ Ozone",
                       value: String(format: "%.0f", reading.o3),
                       unit: "µg/m³",
                       status: "Good", statusColor: LiquidGlass.greenPositive)
            MetricTile(icon: "info.circle", iconColor: LiquidGlass.greenPositive, label: "NO₂",
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

