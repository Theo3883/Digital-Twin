import SwiftUI

struct MetricGrid: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 12), GridItem(.flexible(), spacing: 12)]

        LazyVGrid(columns: columns, spacing: 12) {
            MetricTile(icon: "wind", iconColor: LiquidGlass.tealPrimary, label: "PM2.5",
                       value: reading.pm25.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "µg/m³",
                       status: pm25Status(reading.pm25), statusColor: pm25Color(reading.pm25))
            MetricTile(icon: "wind", iconColor: LiquidGlass.tealPrimary, label: "PM10",
                       value: reading.pm10.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "µg/m³",
                       status: pm10Status(reading.pm10), statusColor: pm10Color(reading.pm10))

            MetricTile(icon: "thermometer.medium", iconColor: LiquidGlass.amberWarning, label: "Temperature",
                       value: reading.temperature.map { String(format: "%.1f", $0) } ?? "--",
                       unit: "°C",
                       status: "Comfortable", statusColor: LiquidGlass.greenPositive)
            MetricTile(icon: "drop.deformed.fill", iconColor: Color(red: 96/255, green: 165/255, blue: 250/255), label: "Humidity",
                       value: reading.humidity.map { String(format: "%.0f", $0) } ?? "--",
                       unit: "%",
                       status: "Optimal", statusColor: LiquidGlass.greenPositive)

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

